using OpenFramework.GameLoop;
using System.Linq;

namespace OpenFramework.Inventory;

/// <summary>
/// Service host-only qui surveille les WorldItem au sol et les regroupe dans des
/// cartons (DroppedInventory) pour limiter le nombre d'objets physiques actifs.
///
/// Declenchement par cellule de grille 3D (taille = ZoneRadius) :
///  - >= SameTypeThreshold WorldItem du meme ItemMetadata dans la cellule
///  - OU >= MixedThreshold WorldItem (tous types confondus) dans la cellule
///
/// Si un DroppedInventory existe deja dans la cellule, on aspire les items dedans.
/// S'il est plein, on en spawne un nouveau. Anti-duplication : reparenting host-side,
/// destruction du WorldItem source seulement apres reparenting reussi.
/// </summary>
public sealed class DropCompactionService : Component
{
	[Property, Group( "Compaction" )]
	public float TickInterval { get; set; } = 2.5f;

	/// <summary>Taille de la cellule de grille en units. 256u ≈ 5m, soit la taille d'une piece.</summary>
	[Property, Group( "Compaction" )]
	public float ZoneRadius { get; set; } = 256f;

	/// <summary>Nombre minimum d'items du meme type dans une cellule pour declencher la compaction.</summary>
	[Property, Group( "Compaction" )]
	public int SameTypeThreshold { get; set; } = 15;

	/// <summary>Nombre minimum d'items (tous types) dans une cellule pour declencher la compaction.</summary>
	[Property, Group( "Compaction" )]
	public int MixedThreshold { get; set; } = 10;

	/// <summary>Capacite (slots) du carton spawne par la compaction.</summary>
	[Property, Group( "Compaction" )]
	public int CartonCapacity { get; set; } = 30;

	[Property, Group( "Debug" )]
	public bool DebugLog { get; set; } = false;

	private TimeSince _timeSinceLastTick = 0f;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _timeSinceLastTick < TickInterval ) return;
		_timeSinceLastTick = 0f;

		Compact();
	}

	private void Compact()
	{
		var allWorldItems = Scene.GetAllComponents<WorldItem>()
			.Where( IsCompactable )
			.ToList();

		if ( allWorldItems.Count < Math.Min( SameTypeThreshold, MixedThreshold ) )
			return;

		// Indexation en grille 3D — cle = coordonnees entieres de la cellule.
		var cells = new Dictionary<(int x, int y, int z), List<WorldItem>>();
		foreach ( var wi in allWorldItems )
		{
			var key = CellKey( wi.WorldPosition );
			if ( !cells.TryGetValue( key, out var list ) )
				cells[key] = list = new List<WorldItem>();
			list.Add( wi );
		}

		foreach ( var (key, items) in cells )
		{
			// Groupes par metadata pour le test "meme type".
			var sameTypeGroups = items
				.GroupBy( wi => wi.Item?.Metadata )
				.Where( g => g.Key != null )
				.ToList();

			bool sameTypeTrigger = sameTypeGroups.Any( g => g.Count() >= SameTypeThreshold );
			bool mixedTrigger = items.Count >= MixedThreshold;

			if ( !sameTypeTrigger && !mixedTrigger ) continue;

			// Si declencheur "mixte" : on aspire tout. Sinon : seulement les types qui
			// franchissent le seuil same-type.
			List<WorldItem> toCompact = mixedTrigger
				? items
				: sameTypeGroups
					.Where( g => g.Count() >= SameTypeThreshold )
					.SelectMany( g => g )
					.ToList();

			if ( DebugLog )
				Log.Info( $"[DropCompaction] cellule {key} : {toCompact.Count} items a compacter (sameType={sameTypeTrigger}, mixed={mixedTrigger})" );

			CompactZone( key, toCompact );
		}
	}

	private (int x, int y, int z) CellKey( Vector3 pos )
	{
		float s = ZoneRadius;
		return (
			(int)MathF.Floor( pos.x / s ),
			(int)MathF.Floor( pos.y / s ),
			(int)MathF.Floor( pos.z / s )
		);
	}

	/// <summary>
	/// Items eligibles : InventoryItem valide, Metadata non-null, non-Unique.
	/// On exclut volontairement les items uniques pour eviter tout risque
	/// d'invariant casse (un container ne peut detenir qu'un exemplaire d'un Unique).
	/// </summary>
	private bool IsCompactable( WorldItem wi )
	{
		if ( !wi.IsValid() ) return false;
		var item = wi.Item;
		if ( item == null || !item.IsValid ) return false;
		if ( item.Metadata == null ) return false;
		if ( item.Metadata.IsUnique ) return false;
		return true;
	}

	private void CompactZone( (int x, int y, int z) cellKey, List<WorldItem> items )
	{
		if ( items.Count == 0 ) return;

		var center = ComputeCenter( items );

		// On cherche un carton existant dans la cellule pour eviter de spammer.
		// Si un carton existe mais est plein, on en spawnera un autre dans la boucle.
		DroppedInventory carton = FindExistingCarton( cellKey );
		if ( carton == null )
		{
			carton = SpawnCarton( center );
			if ( carton == null ) return;
		}

		foreach ( var wi in items )
		{
			if ( !wi.IsValid() ) continue;
			var item = wi.Item;
			if ( item == null || !item.IsValid ) continue;

			// Si le carton courant est plein, on en spawn un nouveau pour ne rien perdre.
			if ( IsCartonFull( carton, item ) )
			{
				carton = SpawnCarton( center );
				if ( carton == null ) return;
			}

			if ( !TransferToCarton( wi, item, carton ) )
				continue;
		}

		// On rafraichit le total synced du(des) carton(s) touche(s).
		// FindExistingCarton + SpawnCarton ne couvrent qu'un carton a la fois ici,
		// mais TransferToCarton n'appelle pas RefreshSyncedTotal — on le fait globalement.
		if ( carton != null && carton.IsValid() && carton.Container != null )
		{
			carton.Container.RefreshSyncedTotal();
			carton.Container.MarkDirty();
		}
	}

	private Vector3 ComputeCenter( List<WorldItem> items )
	{
		var sum = Vector3.Zero;
		int n = 0;
		foreach ( var wi in items )
		{
			if ( !wi.IsValid() ) continue;
			sum += wi.WorldPosition;
			n++;
		}
		return n == 0 ? Vector3.Zero : sum / n;
	}

	/// <summary>
	/// Cherche un DroppedInventory non-mort, non-occupe, dont la position retombe
	/// dans la meme cellule que les items a compacter.
	/// </summary>
	private DroppedInventory FindExistingCarton( (int x, int y, int z) cellKey )
	{
		foreach ( var dropped in Scene.GetAllComponents<DroppedInventory>() )
		{
			if ( !dropped.IsValid() ) continue;
			if ( dropped.IsDeath ) continue; // les sacs de mort ne servent pas de fourre-tout
			if ( dropped.IsBusy ) continue;  // joueur en train de l'utiliser
			if ( dropped.Container == null ) continue;
			if ( CellKey( dropped.WorldPosition ) != cellKey ) continue;
			return dropped;
		}
		return null;
	}

	/// <summary>
	/// Considere un carton comme plein s'il n'a aucun slot libre ET aucun stack
	/// existant du meme meta avec de la place restante. Sinon il peut absorber
	/// au moins une partie de l'item.
	/// </summary>
	private bool IsCartonFull( DroppedInventory carton, InventoryItem incoming )
	{
		if ( carton == null || !carton.IsValid() ) return true;
		var c = carton.Container;
		if ( c == null ) return true;

		if ( c.GetFirstFreeSlot() != -1 ) return false;

		int maxStack = Math.Max( 1, incoming.Metadata.MaxStack );
		if ( maxStack > 1 )
		{
			bool hasRoomInStack = c.Items.Any( x => x.Metadata == incoming.Metadata && x.Quantity < maxStack );
			if ( hasRoomInStack ) return false;
		}

		return true;
	}

	private DroppedInventory SpawnCarton( Vector3 position )
	{
		var con = Constants.Instance;
		if ( con == null || con.BoxPrefab == null )
		{
			Log.Warning( "[DropCompaction] BoxPrefab introuvable dans Constants — compaction abandonnee" );
			return null;
		}

		var go = con.BoxPrefab.Clone( position + Vector3.Up * 8f );
		if ( go == null ) return null;

		var dropped = go.GetComponent<DroppedInventory>();
		if ( dropped == null || dropped.Container == null )
		{
			go.Destroy();
			return null;
		}

		dropped.DisplayName = "Colis";
		dropped.IsDeath = false;
		// SteamId 0 → carton genere par le systeme, ne sera pas nettoye
		// par PlacedPropsCleanup au depart d'un joueur en particulier.
		dropped.DroppedBySteamId = 0;

		if ( dropped.Container.Capacity < CartonCapacity )
			dropped.Container.Capacity = CartonCapacity;

		go.NetworkSpawn();

		if ( DebugLog )
			Log.Info( $"[DropCompaction] Spawn carton @ {position} cap={dropped.Container.Capacity}" );

		return dropped;
	}

	/// <summary>
	/// Transfere l'InventoryItem (enfant du WorldItem) vers le container du carton.
	/// Strategie anti-duplication :
	///   1. On stack d'abord sur un InventoryItem existant du meme meta (pas de nouveau GO).
	///   2. Si reste > 0, on reparente l'InventoryItem.GameObject vers le container.
	///   3. On detruit le WorldItem.GameObject (l'enveloppe physique vide).
	/// On ne detruit JAMAIS la source avant que la destination ait absorbe la quantite.
	/// </summary>
	private bool TransferToCarton( WorldItem wi, InventoryItem item, DroppedInventory carton )
	{
		var meta = item.Metadata;
		var c = carton.Container;
		if ( c == null ) return false;

		int maxStack = Math.Max( 1, meta.MaxStack );
		int remaining = item.Quantity;

		// 1. Stacker sur les existants du meme meta.
		if ( maxStack > 1 )
		{
			foreach ( var existing in c.Items.Where( x => x.Metadata == meta ).ToList() )
			{
				if ( remaining <= 0 ) break;
				int space = maxStack - existing.Quantity;
				if ( space <= 0 ) continue;

				int toMove = Math.Min( space, remaining );
				existing.Quantity += toMove;
				remaining -= toMove;
			}
		}

		// 2. Reste a placer dans un slot libre.
		if ( remaining > 0 )
		{
			int slot = c.GetFirstFreeSlot();
			if ( slot == -1 )
			{
				// Plus de slot dispo : on reflete dans la quantity puis on laisse le WorldItem
				// au sol (sera repris au tick suivant si un nouveau carton est spawne).
				if ( remaining < item.Quantity )
				{
					item.Quantity = remaining;
					return false;
				}
				return false;
			}

			var itemGo = item.GameObject;
			var wiGo = wi.GameObject;

			// Cas defensif : si l'InventoryItem est porte par le meme GameObject que le
			// WorldItem (cf Drop CAS A quand le prefab n'a pas d'InventoryItem enfant pre-cree),
			// reparenter l'item revient a deplacer le WorldItem entier — on ne peut pas detruire
			// son GameObject sans tuer l'item. On retire le component WorldItem et on garde le GO.
			bool sameGo = itemGo == wiGo;

			itemGo.Parent = c.GameObject;
			item.SlotIndex = slot;
			item.Quantity = remaining;

			if ( sameGo )
			{
				// On vire seulement le component WorldItem ; le GameObject heberge maintenant
				// l'InventoryItem rattache au container du carton.
				wi.Destroy();
			}
			else
			{
				// Hierarchie classique : InventoryItem est dans un enfant du WorldItem.
				// On peut detruire l'enveloppe physique sans risque.
				wiGo.Destroy();
			}

			return true;
		}

		// remaining == 0 → tout a ete absorbe en stacking, on detruit l'item source et son enveloppe.
		item.GameObject.Destroy();
		wi.GameObject.Destroy();
		return true;
	}
}
