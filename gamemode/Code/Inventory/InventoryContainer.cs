using Sandbox.Diagnostics;
using Sandbox.Events;
using OpenFramework.Api;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Systems;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFramework.Inventory;

public class InventoryContainer : Component, IGameEventHandler<KillEvent>
{
	// Diagnostic des spawns de sac a la mort (cube blanc, scan voisinage, anomalies).
	// Active via la console: bag_debug 1 (et bag_debug 0 pour eteindre).
	public static bool DebugBagSpawn { get; set; } = false;

	[ConCmd( "bag_debug" )]
	public static void CmdBagDebug( int enable = 1 )
	{
		DebugBagSpawn = enable != 0;
	}

	[Property, Sync( SyncFlags.FromHost )]
	public string Name { get; set; } = "Inventory Container";

	[Property, Sync( SyncFlags.FromHost )]
	public int Capacity { get; set; } = 24;

	/// <summary>
	/// Si activé, le joueur peut ouvrir ce container même s'il est vide (ex: poubelle, coffre).
	/// </summary>
	[Property, Group( "Settings" )]
	public bool AllowOpenWhenEmpty { get; set; } = false;

	// --- AJOUT DU POIDS ---
	[Property, Group( "Limits" ), Sync( SyncFlags.FromHost )]
	public float MaxWeight { get; set; } = 50.0f; // En kg par exemple

	// --- FILTRE ET PLAFOND (sous-conteneurs) ---
	/// <summary>
	/// Si défini, seuls les items de ce type peuvent être ajoutés dans ce conteneur.
	/// Utilisé par les sous-conteneurs (chargeurs n'acceptent que leur type de munition).
	/// </summary>
	[Property, Group( "Limits" )]
	public ItemMetadata AcceptedItemFilter { get; set; }

	/// <summary>
	/// Plafond total d'items (somme des Quantity). 0 = pas de plafond.
	/// Pour un chargeur de 15 balles, MaxTotalItems = 15.
	/// </summary>
	[Property, Group( "Limits" ), Sync( SyncFlags.FromHost )]
	public int MaxTotalItems { get; set; } = 0;

	/// <summary>
	/// Compteur synchronise du total d'items.
	/// Sur serveur dedie, l'enumeration de GameObject.Children cote client ne declenche
	/// pas la regeneration du hash Razor de maniere fiable quand un nouveau InventoryItem
	/// est network-spawne dans un SubContainer (compteur de balles dans l'UI reste a 0
	/// alors que les items existent). Le host met a jour SyncedTotalItemCount via
	/// RefreshSyncedTotal() apres chaque Add/Remove, ce qui garantit la propagation au client.
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public int SyncedTotalItemCount { get; set; } = 0;

	/// <summary>
	/// Somme des Quantity de tous les items dans ce conteneur.
	/// Sur host: recalcule depuis Items. Sur client: utilise la valeur synchronisee.
	/// </summary>
	public int TotalItemCount => Networking.IsHost ? Items.Sum( x => x.Quantity ) : SyncedTotalItemCount;

	/// <summary>
	/// Recalcule SyncedTotalItemCount depuis Items (a appeler host-side apres Add/Remove/Quantity change).
	/// </summary>
	public void RefreshSyncedTotal()
	{
		if ( !Networking.IsHost ) return;
		SyncedTotalItemCount = Items.Sum( x => x.Quantity );
	}

	/// <summary>
	/// Vérifie si un item respecte le filtre du conteneur.
	/// </summary>
	public bool AcceptsItem( ItemMetadata meta )
	{
		if ( AcceptedItemFilter == null ) return true;
		return meta == AcceptedItemFilter;
	}

	/// <summary>
	/// Nombre d'items pouvant encore être ajoutés selon le plafond total.
	/// </summary>
	public int RemainingTotalCapacity => MaxTotalItems > 0
		? Math.Max( 0, MaxTotalItems - TotalItemCount )
		: int.MaxValue;

	/// <summary>
	/// Calcule le poids total actuel du conteneur.
	/// </summary>
	[Property] public float CurrentWeight => Items.Sum( x => x.Quantity * (x.Metadata?.Weight ?? 0f) );

	/// <summary>
	/// Vérifie si un item peut être ajouté sans dépasser le poids maximum.
	/// </summary>
	public bool CanFitWeight( ItemMetadata meta, int quantity = 1 )
	{
		if ( meta == null ) return false;
		return (CurrentWeight + (meta.Weight * quantity)) <= MaxWeight;
	}

	/// <summary>
	/// Vérifie si <paramref name="quantity"/> exemplaires de <paramref name="meta"/> peuvent
	/// tenir dans ce container (poids, plafond total, slots/stacks disponibles).
	/// Ne modifie pas l'état — lecture seule, utilisable avant de valider une transaction.
	/// </summary>
	public bool CanAdd( ItemMetadata meta, int quantity = 1 )
	{
		if ( meta == null || !meta.IsEnabled ) return false;
		if ( !AcceptsItem( meta ) ) return false;
		if ( meta.IsUnique && Items.Any( x => x.Metadata == meta ) ) return false;

		// Poids
		if ( meta.Weight > 0f && (CurrentWeight + meta.Weight * quantity) > MaxWeight ) return false;

		// Plafond total
		if ( MaxTotalItems > 0 && RemainingTotalCapacity < quantity ) return false;

		int remaining = quantity;
		int maxStack = Math.Max( 1, meta.MaxStack );

		// Place dans les stacks existants
		if ( maxStack > 1 )
		{
			foreach ( var item in Items.Where( x => x.Metadata == meta ) )
			{
				remaining -= (maxStack - item.Quantity);
				if ( remaining <= 0 ) return true;
			}
		}

		// Slots libres nécessaires pour le reste
		var occupied = Items.Select( x => x.SlotIndex ).ToHashSet();
		int freeSlots = Enumerable.Range( 0, Capacity ).Count( i => !occupied.Contains( i ) );
		int slotsNeeded = (int)Math.Ceiling( (double)remaining / maxStack );
		return freeSlots >= slotsNeeded;
	}

	public IEnumerable<InventoryItem> Items => GameObject?.Children?
		.Select( x => x.GetComponent<InventoryItem>() )
		.Where( x => x is not null ) ?? Enumerable.Empty<InventoryItem>();

	// ── Dirty flag ────────────────────────────────
	/// <summary>
	/// Indique que le contenu a changé depuis la dernière sauvegarde.
	/// InventoryApiSystem surveille ce flag et sauvegarde périodiquement.
	/// </summary>
	public bool IsDirty { get; private set; } = false;
	public float LastDirtyAt { get; private set; } = 0f;

	/// <summary>
	/// Marque le container comme modifié → sera sauvegardé au prochain cycle.
	/// </summary>
	public void MarkDirty()
	{
		IsDirty = true;
		LastDirtyAt = Time.Now;
	}

	/// <summary>
	/// Appelé par InventoryApiSystem après une sauvegarde réussie.
	/// </summary>
	public void ClearDirty() => IsDirty = false;

	public InventoryContainer() : this( 24 ) { }
	public InventoryContainer( ushort capacity = 24 ) { Capacity = capacity; }

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;

		// On valide les items déjà présents sous le GameObject (Editor-side)
		foreach ( var item in GameObject.Children.Select( x => x.GetComponent<InventoryItem>() ).Where( x => x is not null ) )
		{
			if ( item.SlotIndex < 0 ) item.SlotIndex = GetFirstFreeSlot();

			if ( item.SlotIndex == -1 || item.Metadata == null )
			{
				item.GameObject.Destroy();
				continue;
			}
		}
	}

	public int GetFirstFreeSlot()
	{
		// Toute InventoryItem presente occupe son SlotIndex, y compris les armes
		// equipees (leur InventoryItem reste dans le container meme si l'UI la
		// masque car elle est affichee dans la weapon bar). Sans ca, ejecter un
		// chargeur pouvait creer un item au meme SlotIndex que l'arme equipee,
		// rendant l'un des deux invisible dans la grille (le FirstOrDefault par
		// SlotIndex ne montre qu'un seul des deux).
		var occupied = Items.Select( x => x.SlotIndex ).ToHashSet();
		for ( int i = 0; i < Capacity; i++ )
		{
			if ( !occupied.Contains( i ) ) return i;
		}
		return -1;
	}

	/// <summary>
	/// Restaure les items depuis la base de données via InventoryItemDto (ApiComponent).
	/// Bypass toute la logique de Add — on sait déjà exactement quoi mettre où.
	/// Line/Collum → SlotIndex via la capacité du container (grille en ligne).
	/// Appelé une seule fois par le Host au spawn du joueur.
	/// </summary>
	public void LoadFromDatabase( IEnumerable<InventoryItemDto> items )
	{
		if ( !Networking.IsHost ) return;

		foreach ( var dto in items )
		{
			if ( dto == null ) continue;

			var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == dto.ItemGameId );
			if ( meta == null )
			{
				Log.Warning( $"[Inventory] LoadFromDatabase : metadata '{dto.ItemGameId}' introuvable, item ignoré." );
				continue;
			}

			// Convertit Line/Collum → SlotIndex (stockage en grille côté API)
			// On utilise la même formule que la grille UI : slot = line * colonnes + colonne
			// Si ton API stocke déjà un index linéaire dans Line, Collum vaut 0
			int slotIndex = dto.Line * 6 + dto.Collum; // 6 = nb colonnes de ta grille

			// Vérifie qu'il n'y a pas déjà un item dans ce slot (sécurité)
			if ( Items.Any( x => x.SlotIndex == slotIndex ) )
			{
				Log.Warning( $"[Inventory] LoadFromDatabase : slot {slotIndex} déjà occupé, item '{dto.ItemGameId}' ignoré." );
				continue;
			}

			var go = new GameObject( true );
			go.Parent = GameObject;
			go.Name = $"Item_{meta.Name}";

			var item = go.Components.Create<InventoryItem>();
			item.Metadata = meta;
			item.SlotIndex = slotIndex;
			item.Quantity = Math.Max( 1, dto.Count );

			// Copie les attributs persistes (durabilite, pack_count, current_ammo, etc.)
			if ( dto.Metadata != null )
			{
				foreach ( var attr in dto.Metadata )
					item.Attributes[attr.Key] = attr.Value;
			}

			go.NetworkSpawn();
		}
	}

	[Rpc.Host]
	public static void Add( InventoryContainer container, string itemResource, int quantity = 1, Dictionary<string, string> attributes = null )
	{
		if ( !Networking.IsHost ) return;
		AddInternal( container, itemResource, quantity, attributes, allowOverflowDrop: true );
	}

	/// <summary>
	/// Coeur de la logique d'ajout. Si <paramref name="allowOverflowDrop"/> est vrai et qu'une partie
	/// (ou la totalite) ne rentre pas (filtre, unique, poids, plafond, slots), le surplus est depose
	/// dans un colis au sol via <see cref="DropOverflowAsBox"/> pour ne jamais perdre l'item.
	/// L'appel recursif depuis DropOverflowAsBox passe false pour eviter une boucle infinie.
	/// </summary>
	private static void AddInternal( InventoryContainer container, string itemResource, int quantity, Dictionary<string, string> attributes, bool allowOverflowDrop )
	{
		if ( !Networking.IsHost || container is null ) return;
		if ( quantity <= 0 ) return;

		var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == itemResource );
		if ( meta is null )
		{
			Log.Warning( $"[Inventory.Add] ItemMetadata introuvable pour resource='{itemResource}' (pas dans All, PostLoad non déclenché ?)" );
			return;
		}
		if ( !meta.IsEnabled ) return;

		// --- VERIFICATION FILTRE ---
		// Le container refuse tout l'item (ex: chargeur 9mm vs balle .45). Tout part en colis.
		if ( !container.AcceptsItem( meta ) )
		{
			Log.Warning( $"[Inventory] {meta.Name} n'est pas accepté dans {container.Name} (filtre: {container.AcceptedItemFilter?.Name}) — drop en colis" );
			if ( allowOverflowDrop ) DropOverflowAsBox( container, meta, quantity, attributes );
			return;
		}

		// --- VERIFICATION UNIQUE ---
		if ( meta.IsUnique && container.Items.Any( x => x.Metadata == meta ) )
		{
			Log.Warning( $"[Inventory] {meta.Name} est unique et déjà présent dans {container.Name} — drop en colis" );
			if ( allowOverflowDrop ) DropOverflowAsBox( container, meta, quantity, attributes );
			return;
		}

		int totalRequested = quantity;
		int totalAdded = 0;

		// --- VERIFICATION DU POIDS ---
		// On calcule combien on peut réellement prendre selon le poids restant.
		// Si Weight <= 0, pas de limite par poids (sinon division par zero / item gratuit en poids).
		int possibleByWeight = meta.Weight <= 0f
			? totalRequested
			: (int)MathF.Floor( (container.MaxWeight - container.CurrentWeight) / meta.Weight );
		int actualToAdd = Math.Min( totalRequested, Math.Max( 0, possibleByWeight ) );

		// --- VERIFICATION PLAFOND TOTAL ---
		if ( container.MaxTotalItems > 0 )
			actualToAdd = Math.Min( actualToAdd, container.RemainingTotalCapacity );

		if ( actualToAdd <= 0 )
		{
			Log.Warning( $"[Inventory] {container.GameObject.Name} plein (poids/plafond) pour {meta.Name} — drop en colis" );
			if ( allowOverflowDrop ) DropOverflowAsBox( container, meta, totalRequested, attributes );
			return;
		}

		int remaining = actualToAdd;
		int maxStack = Math.Max( 1, meta.MaxStack );

		// 1. Stack sur l'existant
		if ( maxStack > 1 )
		{
			foreach ( var existingItem in container.Items.Where( x => x.Metadata == meta ) )
			{
				if ( remaining <= 0 ) break;
				int space = maxStack - existingItem.Quantity;
				if ( space <= 0 ) continue;

				int toAdd = Math.Min( space, remaining );
				existingItem.Quantity += toAdd;
				remaining -= toAdd;
				totalAdded += toAdd;
			}
		}

		// 2. Création de nouveaux GameObjects (Seulement si slots dispos)
		while ( remaining > 0 )
		{
			int slot = container.GetFirstFreeSlot();
			if ( slot == -1 ) break;

			var itemGo = new GameObject( true );
			itemGo.Parent = container.GameObject;
			itemGo.Name = $"Item_{meta.Name}";

			var newItem = itemGo.Components.Create<InventoryItem>();
			newItem.Metadata = meta;
			newItem.SlotIndex = slot;

			int stackAmount = Math.Min( remaining, maxStack );
			newItem.Quantity = stackAmount;

			if ( attributes != null )
			{
				foreach ( var attr in attributes )
					newItem.Attributes[attr.Key] = attr.Value;
			}

			itemGo.NetworkSpawn();
			remaining -= stackAmount;
			totalAdded += stackAmount;
		}

		container.RefreshSyncedTotal();
		container.MarkDirty();

		// --- OVERFLOW : ce qui n'a pas pu rentrer (poids/plafond/slots saturés) part en colis ---
		int overflow = totalRequested - totalAdded;
		if ( overflow > 0 && allowOverflowDrop )
		{
			Log.Info( $"[Inventory] {container.Name} : overflow de {overflow}x {meta.Name} → drop en colis" );
			DropOverflowAsBox( container, meta, overflow, attributes );
		}
	}

	/// <summary>
	/// Spawne un colis au sol et y depose <paramref name="quantity"/> instances de <paramref name="meta"/>.
	/// Position du drop : devant le PlayerPawn ancetre du container (achat shop, give admin, loot...),
	/// sinon a la position du container (coffre/distributeur dans le monde). Le proprietaire est
	/// notifie via NotificationSystem si on l'identifie.
	/// </summary>
	private static void DropOverflowAsBox( InventoryContainer container, ItemMetadata meta, int quantity, Dictionary<string, string> attributes )
	{
		if ( !Networking.IsHost ) return;
		if ( container == null || meta == null || quantity <= 0 ) return;

		var con = Constants.Instance;
		if ( con == null || con.BoxPrefab == null )
		{
			Log.Error( $"[Inventory] Pas de BoxPrefab dans Constants — overflow {quantity}x {meta.Name} perdu !" );
			return;
		}

		// Position du carton : devant le joueur si le container appartient a un PlayerPawn.
		// On utilise EyeAngles (synced) + WorldPosition pour ne pas dependre du CameraController
		// (qui peut etre desactive cote serveur dedie pour les pawns d'autres joueurs).
		Vector3 dropPos;
		Client clientToNotify = null;
		var pawn = container.Components.GetInAncestors<PlayerPawn>();
		if ( pawn.IsValid() )
		{
			var eyePos = pawn.WorldPosition + Vector3.Up * 64f;
			var forward = pawn.EyeAngles.ToRotation().Forward;
			var tr = container.Scene.Trace
				.Ray( new Ray( eyePos, forward ), 96f )
				.IgnoreGameObjectHierarchy( pawn.GameObject.Root )
				.WithoutTags( "trigger" )
				.Run();
			dropPos = tr.Hit
				? tr.HitPosition - forward * 16f
				: eyePos + forward * 64f;
			clientToNotify = pawn.Client;
		}
		else
		{
			dropPos = container.WorldPosition + Vector3.Up * 16f;
		}

		var go = con.BoxPrefab.Clone( dropPos + Vector3.Up * 15f );
		if ( go == null )
		{
			Log.Error( $"[Inventory] Echec clone BoxPrefab pour overflow {quantity}x {meta.Name}" );
			return;
		}

		var dropped = go.GetComponent<DroppedInventory>();
		if ( dropped == null || dropped.Container == null )
		{
			Log.Error( "[Inventory] BoxPrefab sans DroppedInventory/Container — annulation overflow" );
			go.Destroy();
			return;
		}

		dropped.DisplayName = "Colis";
		dropped.IsDeath = false;

		// Si l'overflow est massif, on agrandit la capacite du carton pour ne rien reperdre.
		// Estimation grossiere : 1 slot par tranche de MaxStack items.
		int stack = Math.Max( 1, meta.MaxStack );
		int slotsNeeded = (quantity + stack - 1) / stack;
		if ( slotsNeeded > dropped.Container.Capacity )
			dropped.Container.Capacity = slotsNeeded;

		// La box doit exister sur le reseau AVANT que les InventoryItem y soient spawnes.
		go.NetworkSpawn();

		// allowOverflowDrop = false : si la box ne peut pas tout absorber (cas extreme),
		// on log mais on n'enchaine pas une cascade de cartons.
		AddInternal( dropped.Container, meta.ResourceName, quantity, attributes, allowOverflowDrop: false );

		clientToNotify?.Notify( Facepunch.NotificationSystem.NotificationType.Warning,
			$"Pas de place dans l'inventaire — {meta.Name} x{quantity} déposé dans un colis devant vous." );
	}

	/// <summary>
	/// Retire une quantité d'un item (par ResourceName) du container.
	/// Si quantity == -1, retire tout le stack.
	/// </summary>
	[Rpc.Host]
	public static void Remove( InventoryContainer container, string itemResource, int quantity = 1 )
	{
		if ( !Networking.IsHost || container is null ) return;

		var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == itemResource );
		if ( meta is null ) return;

		int remaining = quantity == -1
			? container.Items.Where( x => x.Metadata == meta ).Sum( x => x.Quantity )
			: quantity;

		foreach ( var item in container.Items.Where( x => x.Metadata == meta ).ToList() )
		{
			if ( remaining <= 0 ) break;

			if ( item.Quantity <= remaining )
			{
				remaining -= item.Quantity;
				item.GameObject.Destroy();
			}
			else
			{
				item.Quantity -= remaining;
				remaining = 0;
			}
		}

		if ( remaining > 0 )
			Log.Warning( $"[Inventory] Remove : {remaining}x {meta.Name} introuvable dans {container.Name}" );

		container.RefreshSyncedTotal();
		container.MarkDirty();
	}

	/// <summary>
	/// Vérifie si le container possède au moins <paramref name="quantity"/> d'un item donné.
	/// </summary>
	public static bool Has( InventoryContainer container, string itemResource, int quantity = 1 )
	{
		if ( container is null ) return false;

		var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == itemResource );
		if ( meta is null ) return false;

		int total = container.Items
			.Where( x => x.Metadata == meta )
			.Sum( x => x.Quantity );

		return total >= quantity;
	}

	/// <summary>
	/// Transfère des balles vers un chargeur cible.
	/// Source : pack/boite (attribut pack_count), chargeur (attribut current_ammo), ou item de munitions individuel.
	/// Chargeurs et packs stockent leur contenu dans un attribut (pas de SubContainer).
	/// </summary>
	[Rpc.Host]
	public static void TransferBullets( InventoryItem source, InventoryItem target )
	{
		if ( !Networking.IsHost || source == null || target == null ) return;

		var sourceMeta = source.Metadata;
		var targetMeta = target.Metadata;
		if ( sourceMeta == null || targetMeta == null ) return;
		if ( !targetMeta.IsMagazine ) return;

		// Type de munition que la source peut fournir
		ItemMetadata sourceContentType =
			sourceMeta.IsPack ? sourceMeta.PackContentType :
			sourceMeta.IsMagazine ? sourceMeta.MagAmmoType :
			sourceMeta.IsAmmo ? sourceMeta : null;

		if ( sourceContentType == null || sourceContentType != targetMeta.MagAmmoType )
		{
			Log.Warning( $"[TransferBullets] Types incompatibles (source={sourceContentType?.ResourceName}, target={targetMeta.MagAmmoType?.ResourceName})" );
			return;
		}

		int available =
			sourceMeta.IsPack ? source.PackCount :
			sourceMeta.IsMagazine ? source.MagAmmo :
			source.Quantity;

		int space = targetMeta.MagCapacity - target.MagAmmo;
		int toTransfer = Math.Min( available, space );

		if ( toTransfer <= 0 ) return;

		// Retire coté source
		if ( sourceMeta.IsPack )
		{
			source.PackCount -= toTransfer;
		}
		else if ( sourceMeta.IsMagazine )
		{
			source.MagAmmo -= toTransfer;
		}
		else if ( sourceMeta.IsAmmo )
		{
			source.Quantity -= toTransfer;
			if ( source.Quantity <= 0 ) source.GameObject.Destroy();
		}

		// Ajoute coté cible (attribut, pas de SubContainer)
		target.MagAmmo += toTransfer;
	}

	/// <summary>
	/// Comme TransferBullets, mais affiche une barre de progression chez le client appelant
	/// avant d'effectuer reellement le transfert. Annulable si la source/cible disparait.
	/// </summary>
	[Rpc.Host]
	public static async void TransferBulletsWithProgress( InventoryItem source, InventoryItem target, float duration = 1.5f )
	{
		if ( !Networking.IsHost || source == null || target == null ) { Log.Warning( "[TransferBulletsWithProgress] abandon: host/source/target null" ); return; }
		if ( !CanTransferBullets( source, target ) ) { Log.Warning( "[TransferBulletsWithProgress] CanTransferBullets=false pre-delai" ); return; }

		var caller = Rpc.Caller.GetClient();
		if ( caller != null && duration > 0f )
		{
			await ActionGraphUtility.ItemActionProgress( caller, "Remplissage du chargeur...", duration );
		}

		// Re-verifie les pre-conditions apres le delai (objets peuvent avoir disparu)
		if ( source == null || target == null || !source.IsValid || !target.IsValid ) { Log.Warning( "[TransferBulletsWithProgress] source/target invalid apres delai" ); return; }
		if ( !CanTransferBullets( source, target ) ) { Log.Warning( "[TransferBulletsWithProgress] CanTransferBullets=false post-delai" ); return; }

		TransferBullets( source, target );
	}

	/// <summary>
	/// Vérifie si deux items sont compatibles pour un transfert de balles.
	/// Retourne true si source peut remplir target.
	/// Source supportee : pack/boite, chargeur (attribut), item de munition directe.
	/// </summary>
	public static bool CanTransferBullets( InventoryItem source, InventoryItem target )
	{
		if ( source == null || target == null ) return false;

		var sourceMeta = source.Metadata;
		var targetMeta = target.Metadata;
		if ( sourceMeta == null || targetMeta == null ) return false;
		if ( !targetMeta.IsMagazine ) return false;

		ItemMetadata sourceContentType =
			sourceMeta.IsPack ? sourceMeta.PackContentType :
			sourceMeta.IsMagazine ? sourceMeta.MagAmmoType :
			sourceMeta.IsAmmo ? sourceMeta : null;

		return sourceContentType != null && sourceContentType == targetMeta.MagAmmoType;
	}

	// Verrou anti-cumul cote host : un pawn ne peut consommer qu'un seul item a la
	// fois. Empeche les consommations en parallele quand le joueur clique-droit
	// "Utiliser" sur plusieurs aliments/boissons en succession rapide.
	private static readonly HashSet<Guid> _consumingPawns = new();

	[Rpc.Host]
	public static void Use( InventoryItem item )
	{
		if ( item == null || !item.IsValid ) return;
		var container = item.Components.GetInAncestors<InventoryContainer>();
		_ = UseInternal( item, container );
	}

	private static async Task UseInternal( InventoryItem item, InventoryContainer container )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[Use] UseInternal ignoré : pas le host" );
			return;
		}
		if ( item == null )
		{
			Log.Warning( "[Use] UseInternal ignoré : item null" );
			return;
		}

		var meta = item.Metadata;
		var callerClient = Rpc.Caller.GetClient();
		var pawn = callerClient?.PlayerPawn;

		if ( meta == null || !pawn.IsValid() )
		{
			Log.Warning( $"[Use] Abort : meta={meta == null}, pawn valid={pawn.IsValid()}" );
			return;
		}

		// Verrou anti-cumul pour les consommables : si le pawn consomme deja
		// quelque chose, on rejette pour eviter les buffs/drains en parallele.
		// Les usages non-consommables (equip d'arme) restent libres.
		bool needsConsumeLock = meta.IsConsumable;
		var pawnId = pawn.GameObject.Id;

		if ( needsConsumeLock && !_consumingPawns.Add( pawnId ) )
		{
			Log.Info( $"[Use] {pawn.GameObject.Name} consomme deja — Use ignoré pour {item.Name}" );
			return;
		}

		try
		{
			bool success = true;

			// Armes → GiveWeapon directement sans passer par l'Action Graph
			if ( meta.IsWeapon && meta.WeaponResource != null )
			{
				success = ActionGraphUtility.GiveWeapon( item, pawn, meta.WeaponResource );
			}
			else if ( meta.OnUseAction != null )
			{
				success = await meta.OnUseAction.Invoke( pawn, item );
			}

			if ( success && meta.IsConsumable )
			{
				item.Quantity -= 1;
				container?.MarkDirty();
			}
		}
		finally
		{
			if ( needsConsumeLock )
				_consumingPawns.Remove( pawnId );
		}
	}

	[Rpc.Host]
	public static void Drop( InventoryItem item, Transform transform, int quantity = 1 )
	{
		if ( !Networking.IsHost ) { Log.Warning( "[Inventory] Drop ignoré: Pas le Host" ); return; }
		if ( item == null || !item.IsValid ) { Log.Warning( "[Inventory] Drop ignoré: Item invalide" ); return; }

		// 1. Déterminer la quantité réelle à détacher
		int amountToDrop = (quantity == -1) ? item.Quantity : Math.Min( quantity, item.Quantity );
		if ( amountToDrop <= 0 ) { Log.Warning( $"[Inventory] Quantité de drop invalide: {amountToDrop}" ); return; }

		var meta = item.Metadata;
		var container = item.Components.GetInAncestors<InventoryContainer>();

		if ( container == null )
		{
			Log.Error( $"[Inventory] Impossible de drop: L'item {item.Name} n'a pas de parent InventoryContainer !" );
			return;
		}

		// --- CAS A : DROP D'UN SEUL OBJET (Physique) ---
		if ( amountToDrop == 1 && meta.WorldObjectPrefab != null )
		{
			var worldGo = Spawnable.CreateWithReturnFromHost( meta.WorldObjectPrefab.ResourcePath, transform, Rpc.Caller );
			if ( worldGo != null )
			{
				// S'assurer que l'objet a un InventoryItem avec les bonnes métadonnées
				var newItemData = worldGo.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndDescendants );
				if ( newItemData == null )
				{
					newItemData = worldGo.Components.Create<InventoryItem>();
					newItemData.Metadata = meta;
				}
				newItemData.Quantity = 1;
				newItemData.SlotIndex = -1;
				foreach ( var attr in item.Attributes ) newItemData.Attributes[attr.Key] = attr.Value;

				// Tag pour PlacedPropsCleanup : reecrit a chaque drop, donc si
				// un autre joueur ramasse puis re-drop, la propriete change.
				newItemData.DroppedBySteamId = Rpc.Caller?.SteamId ?? 0;

				// Si l'objet a un FurnitureVisual, s'assurer qu'il est déverrouillé et mobile
				var fv = worldGo.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );
				if ( fv != null )
				{
					fv.IsLocked = false;
					// Un meuble droppe (non figer) doit aussi etre nettoye si le
					// dropper se deconnecte. On reecrit l'auteur a chaque drop.
					fv.PlacedBySteamId = Rpc.Caller?.SteamId ?? 0;
				}

				// S'assurer que le Rigidbody est actif et mobile (pas gelé depuis un state de pose)
				var rb = worldGo.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
				if ( rb != null )
				{
					rb.MotionEnabled = true;
					rb.Gravity = true;
				}

				worldGo.NetworkSpawn();
			}
			else
			{
				Log.Error( "[Inventory] Échec du spawn de l'objet physique !" );
			}
		}
		// --- CAS B : DROP DE PLUSIEURS OBJETS (Box) ---
		else
		{
			List<InventoryItem> itemsToBox = new();

			if ( amountToDrop < item.Quantity )
			{
				var splitItem = item.GameObject.Clone();
				splitItem.Parent = null;
				var splitComp = splitItem.GetComponent<InventoryItem>();
				splitComp.Quantity = amountToDrop;
				itemsToBox.Add( splitComp );
			}
			else
			{
				itemsToBox.Add( item );
			}

			container.CreateDroppedInventory( itemsToBox, transform.Position, false );
		}

		// 3. MISE À JOUR DE L'INVENTAIRE SOURCE
		if ( item.Quantity > amountToDrop )
		{
			item.Quantity -= amountToDrop;
		}
		else
		{
			if ( item.GameObject.Parent == container.GameObject )
				item.GameObject.Destroy();
		}

		// AUDIT : trace le drop. Best-effort, n'altère jamais le comportement.
		try
		{
			var dropper = Rpc.Caller?.SteamId.Value ?? 0;
			if ( dropper != 0 && OpenFramework.Api.ApiComponent.Instance != null )
			{
				_ = OpenFramework.Api.ApiComponent.Instance.LogInventoryTransfer(
					(ulong)dropper, "drop", meta?.ResourceName ?? "", amountToDrop,
					sourceType: "player_inventory", sourceId: container?.Id.ToString(),
					targetType: amountToDrop == 1 && meta?.WorldObjectPrefab != null ? "world" : "dropped_bag" );
			}
		}
		catch ( Exception e ) { Log.Warning( $"[Audit] drop log failed: {e.Message}" ); }
	}

	[Rpc.Host]
	public static void MoveItem( InventoryItem item, InventoryContainer targetContainer, int targetSlot = -1, int quantity = -1, bool forceNewStack = false )
	{
		if ( !Networking.IsHost ) { Log.Warning( "[MoveItem] Not host, abort" ); return; }
		if ( item == null ) { Log.Warning( "[MoveItem] item=null, abort" ); return; }
		if ( targetContainer == null ) { Log.Warning( "[MoveItem] targetContainer=null, abort" ); return; }

		var meta = item.Metadata;
		if ( meta == null ) { Log.Warning( "[MoveItem] item.Metadata=null, abort" ); return; }

		// --- VÉRIFICATION FILTRE ---
		var sourceContainer = item.Components.GetInAncestors<InventoryContainer>();
		if ( !targetContainer.AcceptsItem( meta ) )
		{
			Log.Warning( $"[Inventory] {meta.Name} n'est pas accepté dans {targetContainer.Name} (filtre: {targetContainer.AcceptedItemFilter?.Name})" );
			return;
		}

		// --- VÉRIFICATION CONTAINER CLOTHING ---
		// Le container clothing n'accepte que des items clothing compatibles avec le slot ciblé.
		// Empêche notamment qu'une arme soit déplacée dans un slot vêtement (via un swap CAS 3).
		var targetClothingEquip = targetContainer.Components.GetInAncestorsOrSelf<ClothingEquipment>();
		if ( targetClothingEquip != null && targetClothingEquip.Container == targetContainer )
		{
			if ( !meta.IsClothing || meta.ClothingResource == null )
			{
				Log.Warning( $"[Inventory] {meta.Name} n'est pas un vêtement — refus dans le container clothing" );
				return;
			}

			int intendedSlot = targetSlot == -1 ? (ClothingEquipment.GetSlotForClothing( meta ) is { } s ? (int)s : -1) : targetSlot;
			if ( intendedSlot == -1 || !ClothingEquipment.CanEquipInSlot( meta, intendedSlot ) )
			{
				Log.Warning( $"[Inventory] {meta.Name} ne peut pas aller dans le slot clothing #{intendedSlot} (catégorie incompatible)" );
				return;
			}
		}

		// --- VÉRIFICATION UNIQUE ---
		// On ne vérifie que si l'item change de container
		if ( meta.IsUnique && sourceContainer != targetContainer
			&& targetContainer.Items.Any( x => x.Metadata == meta ) )
		{
			Log.Warning( $"[Inventory] {meta.Name} est unique et déjà présent dans {targetContainer.Name}" );
			return;
		}

		// Quantité réelle à déplacer (-1 = tout)
		int amountToMove = (quantity == -1) ? item.Quantity : Math.Clamp( quantity, 1, item.Quantity );

		// --- VÉRIFICATION DU POIDS ---
		float weightPerUnit = meta.Weight;
		int possibleByWeight = weightPerUnit <= 0f
			? amountToMove
			: (int)MathF.Floor( (targetContainer.MaxWeight - targetContainer.CurrentWeight) / weightPerUnit );

		amountToMove = Math.Min( amountToMove, possibleByWeight );

		// --- VÉRIFICATION PLAFOND TOTAL ---
		if ( targetContainer.MaxTotalItems > 0 )
			amountToMove = Math.Min( amountToMove, targetContainer.RemainingTotalCapacity );

		if ( amountToMove <= 0 )
		{
			Log.Warning( $"[Inventory] {targetContainer.Name} ne peut pas recevoir {meta.Name} (poids ou plafond atteint)" );
			return;
		}

		int maxStack = Math.Max( 1, meta.MaxStack );

		// --- CAS 1 : SLOT CIBLE PRÉCISÉ ET OCCUPÉ PAR LE MÊME TYPE → MERGE ---
		if ( targetSlot != -1 )
		{
			var sameItem = targetContainer.Items.FirstOrDefault( x => x.SlotIndex == targetSlot && x.Metadata == meta );
			if ( sameItem != null && maxStack > 1 )
			{
				int space = maxStack - sameItem.Quantity;
				int toMerge = Math.Min( space, amountToMove );

				if ( toMerge > 0 )
				{
					sameItem.Quantity += toMerge;
					item.Quantity -= toMerge;
					amountToMove -= toMerge;
				}

				if ( amountToMove <= 0 )
				{
					targetContainer.MarkDirty();
					item.Components.GetInAncestors<InventoryContainer>()?.MarkDirty();
					return;
				}
			}
		}

		// --- AUTO-STACK : targetSlot == -1, stackable → remplir d'abord les stacks existants ---
		// forceNewStack = true (split explicite) → on saute cette etape pour creer un nouveau stack
		if ( targetSlot == -1 && maxStack > 1 && !forceNewStack )
		{
			foreach ( var existingItem in targetContainer.Items
				.Where( x => x.Metadata == meta && x != item && x.Quantity < maxStack )
				.ToList() )
			{
				if ( amountToMove <= 0 ) break;

				int space = maxStack - existingItem.Quantity;
				int toMerge = Math.Min( space, amountToMove );

				existingItem.Quantity += toMerge;
				item.Quantity -= toMerge;
				amountToMove -= toMerge;
			}

			if ( amountToMove <= 0 )
			{
				if ( item.Quantity <= 0 )
					item.GameObject.Destroy();
				sourceContainer?.MarkDirty();
				targetContainer.MarkDirty();
				return;
			}
		}

		// --- CAS 2 : DÉPLACEMENT PARTIEL → SPLIT ---
		bool isPartialMove = amountToMove < item.Quantity;

		if ( isPartialMove )
		{
			int actualSlot = targetSlot == -1 ? targetContainer.GetFirstFreeSlot() : targetSlot;
			if ( actualSlot == -1 ) { Log.Warning( "[Inventory] Pas de slot libre pour split." ); return; }

			var occupant = targetContainer.Items.FirstOrDefault( x => x.SlotIndex == actualSlot );
			if ( occupant != null ) { Log.Warning( "[Inventory] Slot cible occupé, impossible de splitter." ); return; }

			var splitGo = new GameObject( true );
			splitGo.Parent = targetContainer.GameObject;
			splitGo.Name = $"Item_{meta.Name}";

			var splitItem = splitGo.Components.Create<InventoryItem>();
			splitItem.Metadata = meta;
			splitItem.SlotIndex = actualSlot;
			splitItem.Quantity = amountToMove;

			foreach ( var attr in item.Attributes )
				splitItem.Attributes[attr.Key] = attr.Value;

			splitGo.NetworkSpawn();

			item.Quantity -= amountToMove;

			targetContainer.MarkDirty();
			item.Components.GetInAncestors<InventoryContainer>()?.MarkDirty();
			return;
		}

		// --- CAS 3 : DÉPLACEMENT TOTAL ---
		{
			int finalSlot = targetSlot == -1 ? targetContainer.GetFirstFreeSlot() : targetSlot;
			if ( finalSlot == -1 ) { Log.Warning( "[Inventory] Pas de place dans le conteneur cible !" ); return; }

			var existingItem = targetContainer.Items.FirstOrDefault( x => x.SlotIndex == finalSlot );
			if ( existingItem != null && existingItem != item )
			{
				existingItem.SlotIndex = item.SlotIndex;
				existingItem.GameObject.Parent = item.GameObject.Parent;
			}

			sourceContainer = item.Components.GetInAncestors<InventoryContainer>();

			item.SlotIndex = finalSlot;
			item.GameObject.Parent = targetContainer.GameObject;
			item.WorldPosition = targetContainer.WorldPosition;

			sourceContainer?.MarkDirty();
			targetContainer.MarkDirty();
		}

		// AUDIT : trace le move (drag&drop, swap, split). Best-effort, après que
		// l'opération soit appliquée. On capture seulement le total déplacé via
		// 'amountToMove' tel qu'évalué au début (peut être un peu approximatif sur les merges,
		// mais suffisant pour repérer un transfert anormal d'item entre 2 containers).
		try
		{
			var actor = Rpc.Caller?.SteamId.Value ?? 0;
			if ( actor != 0 && OpenFramework.Api.ApiComponent.Instance != null )
			{
				_ = OpenFramework.Api.ApiComponent.Instance.LogInventoryTransfer(
					(ulong)actor, "move", meta?.ResourceName ?? "", amountToMove,
					sourceType: sourceContainer == targetContainer ? "self" : "container",
					sourceId:   sourceContainer?.Id.ToString(),
					targetType: "container",
					targetId:   targetContainer?.Id.ToString(),
					metadataJson: $"{{\"slot\":{targetSlot},\"forceNewStack\":{forceNewStack.ToString().ToLower()}}}" );
			}
		}
		catch ( Exception e ) { Log.Warning( $"[Audit] move log failed: {e.Message}" ); }
	}

	[Rpc.Host]
	public void CreateDroppedInventory( List<InventoryItem> items, Vector3 position, bool isDeath = false, string customName = "" )
	{
		if ( !Networking.IsHost ) return;

		var con = Constants.Instance;
		if ( con == null ) { Log.Error( "[Box-Debug] Constants.Instance est NUL !" ); return; }

		var prefab = isDeath ? con.BagPrefab : con.BoxPrefab;
		if ( prefab == null ) { Log.Error( $"[Box-Debug] Prefab {(isDeath ? "Bag" : "Box")} est NUL dans Constants !" ); return; }

		// 1. Clone du prefab
		var go = prefab.Clone( position + Vector3.Up * 15f );
		if ( go == null ) { Log.Error( "[Box-Debug] Le Clone du prefab a échoué !" ); return; }

		// DIAG: voir si le modele resout bien (cube blanc = modele non charge/non monte)
		// Active via la console: bag_debug 1
		if ( DebugBagSpawn )
		{
			Log.Info( $"[Box-Diag] Bag spawn → prefabName={prefab.Name} goName={go.Name} scale={go.WorldScale} pos={go.WorldPosition}" );
			try
			{
				var renderers = go.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ).ToList();
				Log.Info( $"[Box-Diag]   renderers count={renderers.Count}" );
				foreach ( var mr in renderers )
				{
					var modelName = mr.Model?.Name ?? "<null>";
					Log.Info( $"[Box-Diag]   ModelRenderer on '{mr.GameObject.Name}': model={modelName} bounds={mr.Bounds.Size} enabled={mr.Enabled}" );
				}
			}
			catch ( System.Exception ex )
			{
				Log.Warning( $"[Box-Diag] ERREUR pendant le scan des renderers: {ex.Message}" );
			}
		}

		var dropped = go.GetComponent<DroppedInventory>();
		if ( dropped == null ) { Log.Error( "[Box-Debug] Le prefab n'a pas de composant DroppedInventory !" ); return; }

		// On s'assure que le container de la box est prêt
		if ( dropped.Container == null ) { Log.Error( "[Box-Debug] Le DroppedInventory n'a pas de InventoryContainer !" ); return; }

		dropped.DisplayName = isDeath ? "Sac" : "Colis";
		dropped.IsDeath = isDeath;

		// Tag d'origine pour PlacedPropsCleanup. Les sacs de mort restent a 0
		// (gere par leur Timer.HostAfter) — sinon on supprimerait le sac d'un
		// joueur mort qui se deco, alors que d'autres pourraient encore le looter.
		if ( !isDeath )
			dropped.DroppedBySteamId = Rpc.Caller?.SteamId ?? 0;

		// A la mort, on fusionne inventaire principal + vetements (jusqu'a 24 + 10 slots).
		// On agrandit la capacite du sac si necessaire pour eviter qu'un item soit bloque sur le cadavre.
		if ( items.Count > dropped.Container.Capacity )
			dropped.Container.Capacity = items.Count;

		// 2. NetworkSpawn EN PREMIER — la box doit exister sur le réseau
		//    avant qu'on y déplace des items, sinon les clients ne voient
		//    jamais les enfants qui ont été reparentés avant le spawn.
		go.NetworkSpawn();

		if ( isDeath )
		{
			var lifetime = con.DropLifetime;
			Timer.HostAfter( $"bag_despawn_{go.Id}", lifetime, () =>
			{
				if ( go.IsValid() ) go.Destroy();
			} );
		}

		// 3. Transfert des items APRÈS le spawn
		foreach ( var item in items.ToList() )
		{
			if ( item == null || !item.IsValid ) continue;
			MoveItem( item, dropped.Container );
		}

		// DIAG retarde: scan ~2s apres la mort (ragdoll, drop d'arme, deathcam, spawns tardifs).
		// Active via la console: bag_debug 1
		if ( DebugBagSpawn )
			_ = ScanLateAsync( go );
	}

	private static async Task ScanLateAsync( GameObject bag )
	{
		try
		{
			await GameTask.DelaySeconds( 2f );
			if ( !bag.IsValid() ) { Log.Info( "[Box-Scan-Late] sac detruit avant scan" ); return; }
			var bagPos = bag.WorldPosition;
			var allRenderers = Game.ActiveScene.GetAllComponents<ModelRenderer>().ToList();
			Log.Info( $"[Box-Scan-Late] === scan 2s post-mort, total renderers scene={allRenderers.Count} ===" );

			// 1. Tout dans 300u du sac
			foreach ( var mr in allRenderers )
			{
				if ( !mr.IsValid() || mr.GameObject == null ) continue;
				var dist = mr.WorldPosition.Distance( bagPos );
				if ( dist > 300f ) continue;
				var modelName = mr.Model?.Name ?? "<null>";
				var bounds = mr.Bounds.Size;
				var flag = (bounds.x > 50f || bounds.y > 50f || bounds.z > 50f) ? " ⚠ GROS" : "";
				Log.Info( $"[Box-Scan-Late]   dist={dist:F0} name='{mr.GameObject.Name}' model={modelName} bounds={bounds}{flag}" );
			}

			// 2. Anomalies: tout objet avec bounds > 200u OU model null dans toute la scene
			Log.Info( "[Box-Scan-Late] === anomalies (model null OU bounds > 200u) ===" );
			foreach ( var mr in allRenderers )
			{
				if ( !mr.IsValid() || mr.GameObject == null ) continue;
				var bounds = mr.Bounds.Size;
				bool isAnomaly = mr.Model == null || bounds.x > 200f || bounds.y > 200f || bounds.z > 200f;
				if ( !isAnomaly ) continue;
				var modelName = mr.Model?.Name ?? "<null>";
				var dist = mr.WorldPosition.Distance( bagPos );
				Log.Info( $"[Box-Scan-Late]   ⚠ANOMALIE dist={dist:F0} name='{mr.GameObject.Name}' parent='{mr.GameObject.Parent?.Name ?? "<root>"}' model={modelName} bounds={bounds} pos={mr.WorldPosition}" );
			}
		}
		catch ( System.Exception ex )
		{
			Log.Warning( $"[Box-Scan-Late] ERREUR: {ex.Message}" );
		}
	}

	public void OnGameEvent( KillEvent eventArgs )
	{
		// Le drop a la mort est desactive : il a ete deplace dans
		// PlayerPawn.RespawnAtHospitalWithoutItems (timer expire sans EMS,
		// ou respawn manuel via F). Comme ca, si un medic reanime au defib,
		// le joueur garde son inventaire et ses vetements — pas de duplication
		// possible avec la restauration des vetements via SavedClothingJson.
	}
}
