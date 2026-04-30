using Facepunch;
using OpenFramework.Api;
using OpenFramework.Command;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Jobs;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;
using System.Threading.Tasks;

namespace OpenFramework.Systems.Pawn;

public enum RespawnState
{
	Not,
	Requested,
	Delayed,
	Immediate
}

public partial class Client
{
	/// <summary>
	/// The prefab to spawn when we want to make a player pawn for the player.
	/// </summary>
	[Property] public GameObject PlayerPawnPrefab { get; set; }

	public TimeSince TimeSinceRespawnStateChanged { get; private set; }
	public DamageInfo LastDamageInfo { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public Vector3 DeathPosition { get; private set; }

	[Sync( SyncFlags.FromHost )]
	public Vector3 HopitalPosition { get; private set; }

	/// <summary>
	/// Are we ready to respawn?
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnRespawnStateChanged ) )] public RespawnState RespawnState { get; set; }

	public bool IsRespawning => RespawnState is RespawnState.Delayed;

	
	
	private void Spawn( SpawnPointInfo spawnPoint, bool isInitialSpawn = false )
	{
		// Snapshot des Saved* AVANT spawn — si ces valeurs sont les defaults
		// (SavedSkinGroup="default", SavedModelPath="", HasCustomizedAppearance=false)
		// c'est que HydrateAppearanceFromApi n'a PAS ete appele en amont => bug
		// d'apparence garanti.
		Log.Info( $"[AppearanceSync][HOST] Spawn START {DisplayName} initial={isInitialSpawn} — Saved snapshot: IsFemale={SavedIsFemale}, Skin={SavedSkinGroup}, Head={SavedHeadIndex}, ModelPath={SavedModelPath}, HasCustomized={HasCustomizedAppearance}, ClothingJson={(SavedClothingJson?.Length > 80 ? SavedClothingJson.Substring(0,80) + "..." : SavedClothingJson)}" );

		var prefab = PlayerPawnPrefab.Clone( spawnPoint.Transform );
		var pawn = prefab.GetComponent<PlayerPawn>();

		pawn.Client = this;

		pawn.SetSpawnPoint( spawnPoint );

		prefab.NetworkSpawn( Network.Owner );

		PlayerPawn = pawn;

		var appearance = pawn.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndChildren );
		appearance?.SetAppearanceFromServer( SavedIsFemale, SavedMorphsJson, SavedHeadIndex, SavedSkinGroup );

		// Host : restaure immédiatement avec les valeurs explicites (même pour le
		// spawn initial, où HasCustomizedAppearance=false mais où on veut quand
		// même que le host voie le bon skin sur son propre pawn).
		pawn.Body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );

		// Broadcast l'apparence inconditionnellement a tous les clients : depuis que
		// PlayerApiBridge.HydrateAppearanceFromApi remplit Saved* AVANT SpawnPawn,
		// on a toujours skin/sex/modele corrects au spawn. Sans broadcast, le pawn
		// apparait avec le MaterialGroup et le modele par defaut du prefab (homme,
		// skin par defaut) sur les clients distants. La condition HasCustomizedAppearance
		// d'avant ne fonctionnait que si le client avait explicitement appele
		// Manager.ApplyToPlayer() apres le spawn — fragile en serveur dedie.
		if ( !string.IsNullOrEmpty( SavedClothingJson ) && SavedClothingJson != "[]" )
		{
			var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( SavedClothingJson );
			// Passe skin/head/morphs explicites : sur les clients, pawn.Client
			// peut ne pas être encore sync (Sync FromHost) quand le RPC arrive,
			// et RestoreAppearance retomberait alors sur "default".
			Log.Info( $"[AppearanceSync][HOST] Spawn -> BroadcastEquipListWithAppearance {DisplayName}: {paths?.Count ?? 0} clothing path(s), skin={SavedSkinGroup}, model={SavedModelPath}" );
			Client.BroadcastEquipListWithAppearance( pawn.GameObject, paths, false, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );
		}
		else
		{
			// Pas de clothing a reappliquer, mais on force quand meme le skin/modele
			// sur tous les clients pour eviter le MaterialGroup par defaut du prefab.
			Log.Info( $"[AppearanceSync][HOST] Spawn -> BroadcastApplySkin {DisplayName} (no clothing): skin={SavedSkinGroup}, head={SavedHeadIndex}, model={SavedModelPath}" );
			Client.BroadcastApplySkin( pawn.GameObject, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );
		}

		RespawnState = RespawnState.Not;
		Log.Info( $"Player spawned: {GameObject.Name} ({DisplayName}) at {spawnPoint.Position} with tags [{string.Join( ", ", spawnPoint.Tags )}]" );
		pawn.OnRespawn();
		
		Data.Hunger = isInitialSpawn ? 100f : 50f;
		Data.Thirst = isInitialSpawn ? 100f : 50f;
		RpcSetCrosshairVisible( true );

		// Null-safe : si Data.Job est vide (nouveau perso) ou inconnu, GetJob renvoie null.
		// Avant le fix de JobSystem, GetComponentInChildren pouvait aussi NRE en cas de
		// JobSystem absent. Dans les deux cas le NRE interrompait la chaine de spawn et le
		// menu de creation restait bloque sur "CREATION EN COURS". Default sur "citizen".
		var jobName = string.IsNullOrEmpty( Data.Job ) ? "citizen" : Data.Job;
		var job = JobSystem.GetJob( jobName ) ?? JobSystem.GetJob( "citizen" );
		if ( job != null )
		{
			job.OnSpawn( Local, PlayerPawn );
		}
		else
		{
			Log.Warning( $"[Spawning] JobSystem.GetJob('{jobName}') et fallback 'citizen' renvoient null — JobSystem absent de la scene ?" );
		}
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "punch" ) );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "mains" ) );

		// Charge l'inventaire depuis l'API et auto-équipe les armes
		_ = LoadAndEquipInventoryAsync();
	}

	/// <summary>
	/// Charge l'inventaire du joueur depuis l'API, puis équipe automatiquement
	/// toutes les armes présentes.
	/// </summary>
	private async Task LoadAndEquipInventoryAsync()
	{
		Log.Info( $"[Reco:Load] LoadAndEquipInventoryAsync debut pour {DisplayName} (SteamId={SteamId})" );
		if ( !Networking.IsHost || PlayerPawn == null || !PlayerPawn.IsValid() )
		{
			Log.Warning( $"[Reco:Load] Abort load pour {DisplayName}: IsHost={Networking.IsHost}, PlayerPawn valid={PlayerPawn.IsValid()}" );
			return;
		}

		var container = PlayerPawn.InventoryContainer;
		if ( container == null )
		{
			Log.Warning( $"[Reco:Load] InventoryContainer null pour {DisplayName}, load annule" );
			return;
		}

		var apiSystem = InventoryApiSystem.Instance;
		if ( apiSystem == null )
		{
			Log.Warning( "[Reco:Load] InventoryApiSystem introuvable, inventaire non chargé." );
			return;
		}

		await apiSystem.LoadPlayerInventoryAsync( this, container );
		var itemCount = container.Items.Count();
		Log.Info( $"[Reco:Load] LoadPlayerInventoryAsync termine pour {DisplayName}, container a {itemCount} items. AutoEquip..." );

		// Auto-équipe les armes après le chargement
		AutoEquipWeapons( container );
		Log.Info( $"[Reco:Load] LoadAndEquipInventoryAsync fini pour {DisplayName}" );
	}

	/// <summary>
	/// Parcourt l'inventaire et équipe automatiquement toutes les armes.
	/// </summary>
	private void AutoEquipWeapons( InventoryContainer container )
	{
		if ( PlayerPawn == null || !PlayerPawn.IsValid() ) return;

		foreach ( var item in container.Items.ToList() )
		{
			if ( item?.Metadata == null ) continue;
			if ( !item.Metadata.IsWeapon || item.Metadata.WeaponResource == null ) continue;

			// Vérifie que l'arme n'est pas déjà équipée
			var alreadyEquipped = PlayerPawn.Inventory.Equipment
				.Any( e => e.LinkedItem == item );
			if ( alreadyEquipped ) continue;

			// Diag reco: dump des attributs de chargeur AVANT GiveWeapon
			var magType = item.Attributes.GetValueOrDefault( "loaded_mag_type", "" );
			var magAmmo = item.Attributes.GetInt( "loaded_mag_ammo", -1 );
			var magCap = item.Attributes.GetInt( "loaded_mag_capacity", -1 );
			Log.Info( $"[Inventory:Reco] BEFORE GiveWeapon item='{item.Metadata.Name}' attrs: type='{magType}', ammo={magAmmo}, cap={magCap}" );

			ActionGraphUtility.GiveWeapon( item, PlayerPawn, item.Metadata.WeaponResource );

			// Diag reco: verifie l'etat de l'arme/ammo APRES GiveWeapon
			var equipped = PlayerPawn.Inventory.Equipment.FirstOrDefault( e => e.LinkedItem == item );
			var ammo = equipped?.GetComponentInChildren<WeaponAmmo>();
			Log.Info( $"[Inventory:Reco] AFTER GiveWeapon item='{item.Metadata.Name}' equipped={equipped != null}, ammoComp={ammo != null}, Ammo={ammo?.Ammo}, MaxAmmo={ammo?.MaxAmmo}, MagPresent={ammo?.MagPresent}, HasMagazine={ammo?.HasMagazine}" );
		}
	}

	public void Respawn( bool forceNew, bool isInitialSpawn = false )
	{
		// Le spawn initial utilise un random spawn. Pour une reconnexion ou un
		// changement de character, la vraie position (per-character) est
		// restauree juste apres par PlayerApiBridge.SpawnAtLastPosition qui
		// teleporte le pawn a la derniere position stockee cote API pour CE
		// character precis. Ne PAS stocker la position en memoire sur le
		// Client (par SteamId) : ca ferait spawner un autre character sur la
		// position du precedent.
		var spawnPoint = GameUtils.GetRandomSpawnPoint();
		Log.Info( $"[Reco:Spawn] {DisplayName} (SteamId={SteamId}) respawn initial au random spawn {spawnPoint.Position} (forceNew={forceNew}). Position per-character restauree ensuite via API." );

		Log.Info( $"Spawning player.. ( {GameObject.Name} ({DisplayName}), {spawnPoint.Position}, [{string.Join( ", ", spawnPoint.Tags )}] )" );

		if ( Data.Job == "" )
			Data.Job = "citizen"; // reset job on respawn

		if ( forceNew || !PlayerPawn.IsValid() || PlayerPawn.HealthComponent.State == LifeState.Dead )
		{
			if ( PlayerPawn.IsValid() ) PlayerPawn.IsDestroyedForRespawn = true;
			PlayerPawn?.GameObject?.Destroy();
			PlayerPawn = null;

			Spawn( spawnPoint, isInitialSpawn );
		}
		else
		{
			PlayerPawn.SetSpawnPoint( spawnPoint );
			PlayerPawn.OnRespawn();
		}

		// L'inventaire est chargé dans Spawn() via LoadAndEquipInventoryAsync
	}

	public void RespawnInPlaceDeath( bool forceNew, Vector3 position )
	{
		Log.Info( $"[RESPAWN IN PLACE] {DisplayName} à {position}" );

		// Ajouter un petit offset pour éviter d'être dans le sol
		Vector3 safePos = position + Vector3.Up * 8f;
		DeathPosition = safePos;

		// Snapshot de l'inventaire AVANT destruction du pawn : sans ca, le
		// nouveau pawn cree avec un inventaire vide marque son container Dirty
		// (via Give mains/punch) et la boucle InventoryApiSystem ecrase
		// l'inventaire DB du joueur au prochain tick (10s). Resultat : le
		// joueur ressuscite a poil.
		List<InventoryItemDto> mainSnapshot = null;
		List<InventoryItemDto> clothingSnapshot = null;
		if ( Networking.IsHost && PlayerPawn.IsValid() && InventoryApiSystem.Instance != null )
		{
			var oldMain = PlayerPawn.InventoryContainer;
			var oldClothing = PlayerPawn.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren )?.Container;
			if ( oldMain != null )
				mainSnapshot = InventoryApiSystem.Instance.CollectPlayerSnapshot( oldMain, null );
			if ( oldClothing != null )
				clothingSnapshot = InventoryApiSystem.Instance.CollectPlayerSnapshot( null, oldClothing );
			Log.Info( $"[RESPAWN IN PLACE] Snapshot pour {DisplayName}: {mainSnapshot?.Count ?? 0} main, {clothingSnapshot?.Count ?? 0} clothing." );
		}

		// Détruire l'ancien pawn
		if ( forceNew || !PlayerPawn.IsValid() || PlayerPawn.HealthComponent.State == LifeState.Dead )
		{
			if ( PlayerPawn.IsValid() ) PlayerPawn.IsDestroyedForRespawn = true;
			PlayerPawn?.GameObject?.Destroy();
			PlayerPawn = null;
		}

		// Création du nouveau pawn
		var prefab = PlayerPawnPrefab.Clone();
		var pawn = prefab.GetComponent<PlayerPawn>();

		pawn.Client = this;

		// 🚨 OWNER CORRECT !! 🚨
		prefab.NetworkSpawn( Connection );

		// Très important : assigner le pawn !
		PlayerPawn = pawn;

		var appearanceInPlace = pawn.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndChildren );
		appearanceInPlace?.SetAppearanceFromServer( SavedIsFemale, SavedMorphsJson, SavedHeadIndex, SavedSkinGroup );

		pawn.Body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );

		// ✅ Restaure les vêtements
		if ( !string.IsNullOrEmpty( SavedClothingJson ) && SavedClothingJson != "[]" )
		{
			var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( SavedClothingJson );
			Client.BroadcastEquipListWithAppearance( pawn.GameObject, paths, true, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );
		}
		else
		{
			Client.BroadcastApplySkin( pawn.GameObject, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );
		}
		// Appeler l'événement de respawn
		pawn.OnRespawnInPlace();

		RespawnState = RespawnState.Not;
		Data.Hunger = 50f;
		Data.Thirst = 50f;
		RpcSetCrosshairVisible( true );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "mains" ) );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "punch" ) );

		// Restauration de l'inventaire snapshote AVANT destruction. On passe
		// par LoadFromDatabase (memoire, pas API) et on ClearDirty pour eviter
		// que la boucle InventoryApiSystem ne re-save apres coup et n'ecrase
		// quoi que ce soit. AutoEquipWeapons re-equipe les armes du container.
		if ( Networking.IsHost )
		{
			var newMain = pawn.InventoryContainer;
			if ( mainSnapshot != null && mainSnapshot.Count > 0 && newMain != null )
			{
				newMain.LoadFromDatabase( mainSnapshot );
				newMain.ClearDirty();
				Log.Info( $"[RESPAWN IN PLACE] {mainSnapshot.Count} items restaures dans l'inventaire principal de {DisplayName}." );
			}
			var newClothingEquip = pawn.Components.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
			var newClothing = newClothingEquip?.Container;
			if ( clothingSnapshot != null && clothingSnapshot.Count > 0 && newClothing != null )
			{
				// Retire le marker container_type pose par CollectPlayerSnapshot
				// (sinon il finit dans Attributes du nouvel item).
				foreach ( var dto in clothingSnapshot )
					dto.Metadata?.Remove( "container_type" );
				newClothing.LoadFromDatabase( clothingSnapshot );
				newClothing.ClearDirty();
				Log.Info( $"[RESPAWN IN PLACE] {clothingSnapshot.Count} vetements restaures pour {DisplayName}." );

				// Re-applique visuellement les vetements via le Dresser, sinon
				// les items sont dans le container clothing mais le pawn reste
				// en t-shirt par defaut. Async : attend que le Dresser du
				// nouveau pawn soit replique cote clients avant de broadcast.
				_ = InventoryApiSystem.Instance.AutoApplyClothingVisualsAsync( newClothingEquip );
			}
			if ( newMain != null )
				AutoEquipWeapons( newMain );
		}

		// Fix collisions
		pawn.CharacterController.Enabled = false;
		pawn.CharacterController.Enabled = true;
	}

	public void RespawnInHospital( bool forceNew, Vector3 position )
	{
		Vector3 safePos = position + Vector3.Up * 8f;
		HopitalPosition = safePos;

		// Détruire l'ancien pawn
		if ( forceNew || !PlayerPawn.IsValid() || PlayerPawn.HealthComponent.State == LifeState.Dead )
		{
			if ( PlayerPawn.IsValid() ) PlayerPawn.IsDestroyedForRespawn = true;
			PlayerPawn?.GameObject?.Destroy();
			PlayerPawn = null;
		}

		// Création du nouveau pawn
		var prefab = PlayerPawnPrefab.Clone();
		var pawn = prefab.GetComponent<PlayerPawn>();

		pawn.Client = this;

		// 🚨 OWNER CORRECT !! 🚨
		prefab.NetworkSpawn( Connection );

		// Très important : assigner le pawn !
		PlayerPawn = pawn;

		var appearanceHospital = pawn.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndChildren );
		appearanceHospital?.SetAppearanceFromServer( SavedIsFemale, SavedMorphsJson, SavedHeadIndex, SavedSkinGroup );

		Data.Hunger = 50f;
		Data.Thirst = 50f;
		pawn.Body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );

		// NE PAS restaurer les vêtements visuels : le joueur perd tous ses items à l'hôpital.
		// BroadcastEquipList créerait une incohérence (habillé visuellement sans items en inventaire).
		// Mais on force quand même le skin sur tous les clients pour ne pas retomber sur le MaterialGroup par défaut du prefab.
		Client.BroadcastApplySkin( pawn.GameObject, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );

		pawn.OnRespawnInHospital();
		RespawnState = RespawnState.Not;
		RpcSetCrosshairVisible( true );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "mains" ) );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "punch" ) );
		// Fix collisions
		pawn.CharacterController.Enabled = false;
		pawn.CharacterController.Enabled = true;
	}

	public void RespawnInPrison( bool forceNew, Vector3 position )
	{
		Log.Info( $"[RESPAWN IN PLACE] {DisplayName} à {position}" );

		// Ajouter un petit offset pour éviter d'être dans le sol
		Vector3 safePos = position + Vector3.Up * 8f;
		HopitalPosition = safePos;
		// Détruire l'ancien pawn
		if ( forceNew || !PlayerPawn.IsValid() || PlayerPawn.HealthComponent.State == LifeState.Dead )
		{
			if ( PlayerPawn.IsValid() ) PlayerPawn.IsDestroyedForRespawn = true;
			PlayerPawn?.GameObject?.Destroy();
			PlayerPawn = null;
		}

		// Création du nouveau pawn
		var prefab = PlayerPawnPrefab.Clone();
		var pawn = prefab.GetComponent<PlayerPawn>();

		pawn.Client = this;

		// 🚨 OWNER CORRECT !! 🚨
		prefab.NetworkSpawn( Connection );

		// Très important : assigner le pawn !
		PlayerPawn = pawn;

		var appearancePrison = pawn.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndChildren );
		appearancePrison?.SetAppearanceFromServer( SavedIsFemale, SavedMorphsJson, SavedHeadIndex, SavedSkinGroup );

		pawn.Body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );
		Client.BroadcastApplySkin( pawn.GameObject, SavedSkinGroup, SavedHeadIndex, SavedMorphsJson, SavedModelPath );

		Data.Hunger = 50f;
		Data.Thirst = 50f;
		// Appeler l'événement de respawn
		pawn.OnRespawnInHospital();
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "mains" ) );
		PlayerPawn.Inventory.Give( EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "punch" ) );
		// Fix collisions
		pawn.CharacterController.Enabled = false;
		pawn.CharacterController.Enabled = true;
	}


	public void OnKill( DamageInfo damageInfo )
	{
		if ( !Networking.IsHost ) return;

		LastDamageInfo = damageInfo;

		var victim = GameUtils.GetPlayerFromComponent( damageInfo.Victim );
		if ( !victim.IsValid() ) return;

		// Memorise la position de mort. Sans ca, DeathPosition reste a (0,0,0)
		// pour tout le chemin "manual respawn apres timer EMS" (touche F dans
		// DeathOverlay) et le sac de mort spawne au centre de la map = perte
		// totale de l'inventaire. Le chemin defibrillateur (RespawnInPlaceDeath)
		// reecrit cette valeur avec la position du ragdoll au moment du revive.
		DeathPosition = victim.WorldPosition + Vector3.Up * 8f;

		RespawnState = RespawnState.Requested;

		// Si au moins un EMS est en ligne au moment de la mort, on laisse 2:30
		// (EMSWaitDelay) au medecin pour venir reanimer. Sinon, respawn auto rapide
		// (RespawnDelay). Le delai retenu est expose via RespawnTotalDelay pour
		// que l'UI puisse afficher la barre de progression coherente cote client.
		int emsOnline = PlayerPawn.GetEMSOnline();
		float delay = emsOnline > 0
			? Constants.Instance.EMSWaitDelay
			: Constants.Instance.RespawnDelay;

		RespawnTotalDelay = delay;
		RespawnEndTime = Time.Now + delay;
		RpcSetCrosshairVisible( false );
		Log.Info( $"[OnKill] {DisplayName} mort. EMS en ligne={emsOnline}. Respawn possible dans {delay}s" );

		// Plus de Timer ni de notification ici —
		// UpdateDeadRespawnLogic() dans PlayerPawn_State.cs gère le reste
	}

	protected void OnRespawnStateChanged( RespawnState oldValue, RespawnState newValue )
	{
		// On réinitialise le timer de changement d'état
		TimeSinceRespawnStateChanged = 0f;

		// Log pour confirmer que l'erreur est résolue
		Log.Info( $"RespawnState changé : {oldValue} -> {newValue}" );
	}

	public PlayerPawn GetLastKiller()
	{
		return GameUtils.GetPlayerFromComponent( LastDamageInfo?.Attacker );
	}

	protected void VerifyRespawn()
	{
		Log.Info( RespawnState );

		if ( RespawnState == RespawnState.Not )
			return;

		// On récupère le temps restant pour le respawn
		float remaining = GetRemaining( RespawnEndTime );

		// Si on est en mode retardé et que le temps est écoulé (<= 0), ou en mode immédiat
		if ( (RespawnState == RespawnState.Delayed && remaining <= 0) || RespawnState == RespawnState.Immediate )
		{
			Respawn( false );
		}
	}

	[Rpc.Owner]
	public void RpcSetCrosshairVisible( bool visible )
	{
		Crosshair.SetVisible( visible );
	}
}
