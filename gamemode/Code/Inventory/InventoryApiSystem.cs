using OpenFramework.Api;
using OpenFramework.Inventory;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;
using System.Linq;
using System.Threading.Tasks;

namespace OpenFramework.Systems;

/// <summary>
/// Surveille tous les InventoryContainer marqués dirty et les sauvegarde
/// périodiquement via ApiComponent.
///
/// Stratégie : dirty flag + save toutes les SaveIntervalSeconds secondes.
/// Les deux containers joueur (inventaire principal + vêtements) sont sauvegardés
/// ensemble pour éviter que ClearInventory n'écrase l'un avec l'autre.
/// </summary>
public class InventoryApiSystem : Component
{
	public static InventoryApiSystem Instance { get; private set; }

	/// <summary>Intervalle entre deux sauvegardes d'un même container (secondes).</summary>
	[Property] public float SaveIntervalSeconds { get; set; } = 10f;

	/// <summary>Nombre de colonnes de la grille inventaire (pour SlotIndex ↔ Line/Collum).</summary>
	public const int GridColumns = 6;

	/// <summary>Clé metadata pour distinguer les items du container clothing.</summary>
	private const string ContainerTypeKey = "container_type";
	private const string ContainerTypeClothing = "clothing";

	private bool _isSaving = false;

	/// <summary>
	/// Snapshots de sauvegarde en cours par SteamId.
	/// Permet a LoadPlayerInventoryAsync d'attendre la fin d'une SaveSnapshotAsync
	/// declenchee a la deconnexion precedente, evitant de lire un inventaire vide
	/// ou partiellement efface (ClearInventory termine mais AddItem pas encore).
	/// </summary>
	private readonly Dictionary<ulong, Task> _pendingSnapshotSaves = new();

	/// <summary>
	/// Retourne la task de sauvegarde snapshot en cours pour ce SteamId,
	/// ou null si aucune. A await par PlayerPawn.OnDestroy avant RemoveToken
	/// pour eviter que le token API soit revoque au milieu d'une boucle
	/// AddInventoryItem (=> perte d'items).
	/// </summary>
	public Task GetPendingSnapshotSave( ulong steamId )
	{
		if ( _pendingSnapshotSaves.TryGetValue( steamId, out var task ) && task != null && !task.IsCompleted )
			return task;
		return null;
	}

	protected override void OnAwake() => Instance = this;

	// ─────────────────────────────────────────────
	//  BOUCLE PRINCIPALE
	// ─────────────────────────────────────────────

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost || _isSaving ) return;

		// Cherche tous les containers joueur dirty
		var dirtyContainers = Game.ActiveScene
			.GetAllComponents<InventoryContainer>()
			.Where( x => x.IsDirty && (Time.Now - x.LastDirtyAt) >= SaveIntervalSeconds )
			.ToList();

		if ( dirtyContainers.Count == 0 ) return;

		// Regroupe par joueur pour sauvegarder les deux containers en une seule fois
		var playerGroups = new Dictionary<ulong, Client>();

		foreach ( var container in dirtyContainers )
		{
			var pawn = container.GameObject.Components.GetInAncestors<PlayerPawn>( true );
			var client = pawn?.Client;
			if ( client != null && !playerGroups.ContainsKey( client.SteamId ) )
				playerGroups[client.SteamId] = client;

			// Containers monde (coffres, etc.)
			var provider = container.GameObject.Components.GetInAncestors<IInventoryProvider>( true );
			if ( provider != null && client == null )
			{
				_isSaving = true;
				_ = SaveWorldAndClearAsync( provider, container );
				return;
			}
		}

		if ( playerGroups.Count == 0 ) return;

		_isSaving = true;
		_ = SaveAllDirtyPlayersAsync( playerGroups, dirtyContainers );
	}

	private async Task SaveWorldAndClearAsync( IInventoryProvider provider, InventoryContainer container )
	{
		await SaveWorldInventoryAsync( provider );
		container.ClearDirty();
		_isSaving = false;
	}

	private async Task SaveAllDirtyPlayersAsync( Dictionary<ulong, Client> playerGroups, List<InventoryContainer> dirtyContainers )
	{
		foreach ( var kvp in playerGroups )
		{
			var client = kvp.Value;
			await SavePlayerFullAsync( client );

			// Clear dirty sur tous les containers de ce joueur
			foreach ( var c in dirtyContainers )
			{
				var pawn = c.GameObject.Components.GetInAncestors<PlayerPawn>( true );
				if ( pawn?.Client == client )
					c.ClearDirty();
			}
		}
		_isSaving = false;
	}

	// ─────────────────────────────────────────────
	//  CHARGEMENT
	// ─────────────────────────────────────────────

	/// <summary>
	/// Charge l'inventaire complet d'un joueur depuis l'API,
	/// en répartissant les items entre inventaire principal et container clothing.
	/// </summary>
	public async Task LoadPlayerInventoryAsync( Client client, InventoryContainer mainContainer )
	{
		if ( !Networking.IsHost || client == null || mainContainer == null ) return;

		var api = ApiComponent.Instance;
		if ( api == null || !api.IsAuthenticated( client.SteamId ) )
		{
			Log.Warning( $"[InventoryApi] {client.DisplayName} non authentifié, inventaire non chargé." );
			return;
		}

		// Attend la fin d'une eventuelle sauvegarde snapshot declenchee a la
		// deconnexion precedente du meme SteamId. Sans cette attente, le GET
		// pourrait tomber entre ClearInventory et AddInventoryItem => inventaire
		// vide ou partiel cote client a la reconnexion.
		if ( _pendingSnapshotSaves.TryGetValue( client.SteamId, out var pending ) && pending != null && !pending.IsCompleted )
		{
			Log.Info( $"[Reco:Load] Attente save snapshot precedente pour {client.DisplayName} avant GET inventaire..." );
			try { await pending; }
			catch ( Exception ex ) { Log.Warning( $"[Reco:Load] Snapshot save precedent a echoue pour {client.DisplayName}: {ex.Message}" ); }
			Log.Info( $"[Reco:Load] Save snapshot precedente terminee, on peut GET pour {client.DisplayName}" );
		}
		else
		{
			Log.Info( $"[Reco:Load] Aucune save snapshot en cours pour {client.DisplayName}, GET direct." );
		}

		Log.Info( $"[InventoryApi] Chargement inventaire joueur {client.DisplayName}..." );

		var allItems = await api.GetInventory( client.SteamId );
		if ( allItems == null || allItems.Count == 0 )
		{
			Log.Info( $"[InventoryApi] Inventaire vide pour {client.DisplayName}" );
			return;
		}

		// Sépare les items par type de container
		var mainItems = new List<InventoryItemDto>();
		var clothingItems = new List<InventoryItemDto>();

		foreach ( var dto in allItems )
		{
			if ( dto.Metadata != null && dto.Metadata.TryGetValue( ContainerTypeKey, out var type ) && type == ContainerTypeClothing )
				clothingItems.Add( dto );
			else
				mainItems.Add( dto );
		}

		// Charge l'inventaire principal
		if ( mainItems.Count > 0 )
		{
			mainContainer.LoadFromDatabase( mainItems );
			Log.Info( $"[InventoryApi] {mainItems.Count} items chargés dans l'inventaire principal de {client.DisplayName}" );
		}

		// Charge les vêtements équipés
		if ( clothingItems.Count > 0 )
		{
			var clothingEquip = client.PlayerPawn?.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
			if ( clothingEquip?.Container != null )
			{
				// Retire le marker container_type avant de charger
				foreach ( var dto in clothingItems )
					dto.Metadata?.Remove( ContainerTypeKey );

				clothingEquip.Container.LoadFromDatabase( clothingItems );
				Log.Info( $"[InventoryApi] {clothingItems.Count} vêtements équipés chargés pour {client.DisplayName}" );

				// Reconstruit Client.SavedClothingJson cote host des le chargement de
				// l'inventaire. Sans ca, lors du prochain respawn (mort), Spawn() lit
				// SavedClothingJson="[]" et ne broadcast aucun vetement aux clients :
				// le joueur ressuscite a poil meme si son container clothing API
				// contient les bons items. Ce JSON sert aussi de source de verite pour
				// les late-joiners via le [Change] callback de SavedClothingJson.
				RebuildSavedClothingJsonOnHost( client, clothingEquip );

				// Applique visuellement chaque vêtement (avec retries pour serveur dédié :
				// le pawn vient d'être NetworkSpawn et son Dresser n'est pas forcément
				// répliqué sur les clients lors du tout premier broadcast).
				_ = AutoApplyClothingVisualsAsync( clothingEquip );
			}
		}
	}

	/// <summary>
	/// Reconstruit Client.SavedClothingJson cote host depuis les ResourcePath des
	/// ClothingResource du container d'equipement. Idempotent et safe a appeler
	/// apres LoadFromDatabase. Cote client (proxy), le [Change] de SavedClothingJson
	/// se chargera ensuite de rebuild le dresser via ApplyAppearanceFromSync.
	/// </summary>
	private static void RebuildSavedClothingJsonOnHost( Client client, ClothingEquipment clothingEquip )
	{
		if ( !Networking.IsHost || client == null || clothingEquip?.Container == null ) return;

		var paths = clothingEquip.Container.Items
			.Where( i => i?.Metadata?.IsClothing == true && i.Metadata.ClothingResource != null )
			.Select( i => i.Metadata.ClothingResource.ResourcePath )
			.ToList();

		client.SavedClothingJson = System.Text.Json.JsonSerializer.Serialize( paths );
		Log.Info( $"[AppearanceSync][HOST] RebuildSavedClothingJson {client.DisplayName}: {paths.Count} vetement(s) -> {client.SavedClothingJson}" );
	}

	/// <summary>
	/// Applique visuellement tous les vêtements présents dans le container clothing.
	/// Sur serveur dédié, on retry plusieurs frames car le Dresser du pawn peut
	/// ne pas être encore prêt sur les clients juste après le NetworkSpawn.
	/// </summary>
	public async Task AutoApplyClothingVisualsAsync( ClothingEquipment clothingEquip )
	{
		if ( clothingEquip?.Container == null ) return;

		var pawn = clothingEquip.Components.GetInAncestorsOrSelf<PlayerPawn>();
		if ( pawn == null ) return;

		// Attend que le Dresser du pawn soit présent côté host (sécurité)
		const int MaxWaitFrames = 30;
		for ( int i = 0; i < MaxWaitFrames; i++ )
		{
			var dresser = pawn.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
			if ( dresser != null ) break;
			await GameTask.DelayRealtime( 50 );
		}

		// Petit délai supplémentaire pour laisser la réplication réseau atteindre
		// les clients distants avant de broadcaster (sinon ils reçoivent l'event
		// avant d'avoir le pawn en local et le BroadcastEquip n'a aucun effet).
		await GameTask.DelayRealtime( 250 );

		if ( clothingEquip?.Container == null || !pawn.IsValid() ) return;

		foreach ( var item in clothingEquip.Container.Items.ToList() )
		{
			if ( item?.Metadata?.IsClothing != true || item.Metadata.ClothingResource == null ) continue;

			Client.BroadcastEquip( pawn.GameObject, item.Metadata.ClothingResource.ResourcePath, Color.White );
			Log.Info( $"[InventoryApi] Vêtement restauré: {item.Metadata.Name}" );
		}
	}

	public async Task LoadWorldInventoryAsync( IInventoryProvider provider )
	{
		if ( !Networking.IsHost || provider == null ) return;
		// TODO : endpoint /inventory/{containerId}/get
		Log.Info( $"[InventoryApi] LoadWorldInventory '{provider.ContainerId}' (non implémenté)" );
		await Task.CompletedTask;
	}

	// ─────────────────────────────────────────────
	//  SAUVEGARDE EXPLICITE (déconnexion, mort...)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Force une sauvegarde immédiate — à appeler à la déconnexion ou à la mort.
	/// Sauvegarde les deux containers (inventaire + vêtements) ensemble.
	/// </summary>
	public async Task ForceSaveAsync( Client client, InventoryContainer container )
	{
		await SavePlayerFullAsync( client );
		container.ClearDirty();

		// Clear dirty aussi sur le clothing container
		var clothingEquip = client.PlayerPawn?.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		clothingEquip?.Container?.ClearDirty();
	}

	// ─────────────────────────────────────────────
	//  INTERNALS
	// ─────────────────────────────────────────────

	/// <summary>
	/// Sauvegarde les deux containers joueur (inventaire + vêtements) en un seul ClearInventory + AddItem.
	/// </summary>
	private async Task SavePlayerFullAsync( Client client )
	{
		if ( client == null || !client.PlayerPawn.IsValid() ) return;

		var api = ApiComponent.Instance;
		if ( api == null || !api.IsAuthenticated( client.SteamId ) ) return;

		Log.Info( $"[InventoryApi] Sauvegarde complète pour {client.DisplayName}..." );

		// Collecte tous les items des deux containers
		var allDtos = new List<InventoryItemDto>();

		// 1. Inventaire principal
		var mainContainer = client.PlayerPawn.InventoryContainer;
		if ( mainContainer != null )
		{
			foreach ( var item in mainContainer.Items.ToList() )
			{
				if ( item?.Metadata == null ) continue;
				allDtos.Add( ToDto( item, isClothing: false ) );
			}
		}

		// 2. Container vêtements
		var clothingEquip = client.PlayerPawn.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		if ( clothingEquip?.Container != null )
		{
			foreach ( var item in clothingEquip.Container.Items.ToList() )
			{
				if ( item?.Metadata == null ) continue;
				allDtos.Add( ToDto( item, isClothing: true ) );
			}
		}

		// Clear + save tout d'un coup
		await api.ClearInventory( client.SteamId );

		foreach ( var dto in allDtos )
			await api.AddInventoryItem( client.SteamId, dto );

		Log.Info( $"[InventoryApi] {allDtos.Count} items sauvegardés pour {client.DisplayName}" );
	}

	private async Task SaveWorldInventoryAsync( IInventoryProvider provider )
	{
		// TODO : endpoint /inventory/{containerId}/save
		Log.Info( $"[InventoryApi] SaveWorldInventory '{provider.ContainerId}' (non implémenté)" );
		await Task.CompletedTask;
	}

	private static InventoryItemDto ToDto( InventoryItem item, bool isClothing )
	{
		// Tous les etats (pack_count, current_ammo, durability, etc.) vivent dans
		// Attributes — plus aucun sous-conteneur a serialiser separement.
		var metadata = item.Attributes.ToDictionary( x => x.Key, x => x.Value );

		// Marque les items clothing pour pouvoir les séparer au chargement
		if ( isClothing )
			metadata[ContainerTypeKey] = ContainerTypeClothing;

		return new InventoryItemDto
		{
			ItemGameId = item.Metadata.ResourceName,
			Count      = item.Quantity,
			Mass       = item.Metadata.Weight,
			Line       = item.SlotIndex / GridColumns,
			Collum     = item.SlotIndex % GridColumns,
			Metadata   = metadata,
		};
	}

	// ─────────────────────────────────────────────
	//  SNAPSHOT (collecte synchrone pour déconnexion)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Collecte l'état actuel de l'inventaire de manière synchrone.
	/// À appeler AVANT de détruire le PlayerPawn pour éviter la perte de données.
	/// </summary>
	public List<InventoryItemDto> CollectPlayerSnapshot( InventoryContainer mainContainer, InventoryContainer clothingContainer )
	{
		var dtos = new List<InventoryItemDto>();

		if ( mainContainer != null )
		{
			foreach ( var item in mainContainer.Items.ToList() )
			{
				if ( item?.Metadata == null ) continue;
				dtos.Add( ToDto( item, isClothing: false ) );
			}
		}

		if ( clothingContainer != null )
		{
			foreach ( var item in clothingContainer.Items.ToList() )
			{
				if ( item?.Metadata == null ) continue;
				dtos.Add( ToDto( item, isClothing: true ) );
			}
		}

		return dtos;
	}

	/// <summary>
	/// Sauvegarde un snapshot pré-collecté — ne dépend pas du PlayerPawn.
	/// </summary>
	public Task SaveSnapshotAsync( Client client, List<InventoryItemDto> snapshot )
	{
		if ( client == null ) return Task.CompletedTask;

		var steamId = client.SteamId;
		var displayName = client.DisplayName;

		// Chaine sur la save precedente du meme joueur pour serialiser les
		// ClearInventory/AddItem (evite que deux saves s'entrelacent).
		_pendingSnapshotSaves.TryGetValue( steamId, out var previous );
		var task = SaveSnapshotInternalAsync( steamId, displayName, snapshot, previous );
		_pendingSnapshotSaves[steamId] = task;

		// Nettoie l'entree quand fini (si personne d'autre n'a pris la place entre-temps)
		_ = task.ContinueWith( t =>
		{
			if ( _pendingSnapshotSaves.TryGetValue( steamId, out var current ) && current == t )
				_pendingSnapshotSaves.Remove( steamId );
		} );

		return task;
	}

	private async Task SaveSnapshotInternalAsync( ulong steamId, string displayName, List<InventoryItemDto> snapshot, Task previous )
	{
		if ( previous != null && !previous.IsCompleted )
		{
			try { await previous; }
			catch { /* ignore: la save suivante recouvre de toute facon */ }
		}

		var api = ApiComponent.Instance;
		if ( api == null || !api.IsAuthenticated( steamId ) ) return;

		Log.Info( $"[Reco:Save] Debut SaveSnapshot pour {displayName} (SteamId={steamId}): {snapshot.Count} items. ClearInventory..." );

		await api.ClearInventory( steamId );
		Log.Info( $"[Reco:Save] ClearInventory fini pour {displayName}, ajout des items..." );

		int done = 0;
		foreach ( var dto in snapshot )
		{
			await api.AddInventoryItem( steamId, dto );
			done++;
		}

		Log.Info( $"[Reco:Save] Snapshot termine pour {displayName}: {done}/{snapshot.Count} items persistes." );
	}
}
