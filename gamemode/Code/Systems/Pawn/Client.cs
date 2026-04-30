using Facepunch;
using OpenFramework.Api;
using System.Collections.Generic;

namespace OpenFramework.Systems.Pawn;

public partial class Client : Component
{
	/// <summary>
	/// The player we're currently in the view of (clientside).
	/// Usually the local player, apart from when spectating etc.
	/// </summary>
	public static Client Viewer { get; private set; }

	/// <summary>
	/// Our local player on this client.
	/// </summary>
	public static Client Local { get; private set; }

	/// <summary>
	/// Who owns this player state?
	/// </summary>
	[Sync( SyncFlags.FromHost ), Property] public ulong SteamId { get; set; }

	/// <summary>
	/// The player's name, which might have to persist if they leave
	/// </summary>
	[Sync( SyncFlags.FromHost )] public string SteamName { get; set; }

	/// <summary>
	/// The connection of this player
	/// </summary>
	public Connection Connection => Network.Owner;

	/// <summary>
	/// Is this player connected? Clients can linger around in competitive matches to keep a player's slot in a team if they disconnect.
	/// </summary>
	public bool IsConnected => Connection is not null && (Connection.IsActive || Connection.IsHost); //smh

	private string name => SteamName ?? "";
	/// <summary>
	/// Name of this player (RP name if available, otherwise Steam name)
	/// </summary>
	public string DisplayName
	{
		get
		{
			var rpName = Data != null && !string.IsNullOrWhiteSpace( Data.FirstName )
				? $"{Data.FirstName} {Data.LastName}".Trim()
				: name;
			return $"{rpName}{(!IsConnected ? " (Disconnected)" : "")}";
		}
	}

	/// <summary>
	/// What's our loadout?
	/// </summary>
	[RequireComponent] public PlayerLoadout Loadout { get; private set; }

	/// <summary>
	/// Are we in the view of this player (clientside)
	/// </summary>
	public bool IsViewer => Viewer == this;

	/// <summary>
	/// Is this the local player for this client
	/// </summary>
	public bool IsLocalPlayer => !IsProxy && Connection == Connection.Local;


	
	/// <summary>
	/// Champs source de verite pour la replication d'apparence. Quand le host
	/// les modifie (HydrateAppearanceFromApi, RebuildSavedClothingJsonOnHost,
	/// JobSystem...), la sync engine les replique a tous les clients (presents
	/// ET late-joiners) — le [Change] callback reconstruit alors le dresser
	/// local et appelle Apply(). C'est pour ca que les Rpc.Broadcast
	/// d'equipement ne suffisent pas seuls : ils ne sont pas rejoues pour un
	/// client qui rejoint apres l'equipement.
	///
	/// SyncFlags.FromHost est OBLIGATOIRE : sans ce flag, les ecritures host
	/// sur le Client d'un autre joueur (owner=ce joueur) ne sont PAS repliquees
	/// aux autres clients — bug "joueur A se voit habille mais joueur B le voit
	/// nu" car le [Change] ne tire que chez le owner et le host. Avec FromHost,
	/// le host devient autoritaire et la valeur est repliquee a tous les
	/// proxies, declenchant le [Change] partout.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedSkinGroup { get; set; } = "default";
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public int SavedHeadIndex { get; set; } = 0;
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedMorphsJson { get; set; } = "{}";
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedClothingJson { get; set; } = "[]";
	[Sync( SyncFlags.FromHost )] public string SavedPersonalClothingJson { get; set; } = "[]";
	[Sync( SyncFlags.FromHost )] public bool SavedIsFemale { get; set; } = false;
	/// <summary>
	/// ResourcePath du modele (male/female) applique au pawn. Sans ca, au respawn
	/// le prefab est clone avec son modele par defaut (masculin) et on perd la
	/// silhouette feminine cree dans le character creator.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedModelPath { get; set; } = "";

	[Sync( SyncFlags.FromHost )] public bool HasCustomizedAppearance { get; set; } = false;

	/// <summary>
	/// Apparence "coiffure" geree par le PNJ Coiffeur. Persistee en API
	/// (Character.HairColor / BeardColor / HairStyle / BeardStyle), hydratee
	/// au spawn par PlayerApiBridge.HydrateAppearanceFromApi, et propagee a
	/// tous les clients via SyncFlags.FromHost (meme logique que les autres
	/// champs Saved* — voir gros commentaire au-dessus).
	///
	/// HairStyle / BeardStyle = ResourcePath du Clothing equipement (les
	/// items "cheveux"/"barbe" passent deja par le dresser et SavedClothingJson,
	/// ces champs servent juste de "memo" pour le coiffeur — le reequipement
	/// effectif passe par Loadout.GivePersonalClothing comme pour les autres
	/// vetements).
	///
	/// HairColor / BeardColor = hex "#RRGGBB". Applique en tant que tint
	/// sur les renderers de cheveux/barbe via PlayerBody.ApplyHairColor.
	/// </summary>
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedHairColor { get; set; } = "#3a2a1c";
	[Sync( SyncFlags.FromHost ), Change( nameof( OnAppearanceSyncChanged ) )] public string SavedBeardColor { get; set; } = "#3a2a1c";
	[Sync( SyncFlags.FromHost )] public string SavedHairStyle { get; set; } = "";
	[Sync( SyncFlags.FromHost )] public string SavedBeardStyle { get; set; } = "";

	private bool _applyAppearanceScheduled = false;

	/// <summary>
	/// Appelé quand n'importe lequel des champs d'apparence synced change. On
	/// schedule un Apply unique au prochain tick pour coalescer les multiples
	/// changements (skin + clothing + morphs arrivent souvent ensemble).
	/// </summary>
	private void OnAppearanceSyncChanged( string oldValue, string newValue ) => ScheduleApplyAppearanceFromSync();
	private void OnAppearanceSyncChanged( int oldValue, int newValue ) => ScheduleApplyAppearanceFromSync();

	private async void ScheduleApplyAppearanceFromSync()
	{
		if ( _applyAppearanceScheduled ) return;
		_applyAppearanceScheduled = true;
		try
		{
			// Petit delai pour coalescer + laisser le temps au PlayerPawn replication
			// (sur late-join, SavedClothingJson peut arriver avant que pawn soit ready).
			await GameTask.DelayRealtimeSeconds( 0.1f );
			if ( !this.IsValid() ) return;
			ApplyAppearanceFromSync();
		}
		finally
		{
			_applyAppearanceScheduled = false;
		}
	}

	/// <summary>
	/// Reconstruit le dresser et l'apparence du PlayerPawn a partir des champs
	/// [Sync] (SavedClothingJson, SavedSkinGroup, etc). Tourne sur chaque client
	/// (proxy comme owner) — c'est ce qui rend la sync complete sans dependre
	/// d'un Rpc.Broadcast.
	/// </summary>
	public void ApplyAppearanceFromSync()
	{
		if ( !this.IsValid() ) return;
		var pawn = PlayerPawn;
		if ( !pawn.IsValid() )
		{
			Log.Warning( $"[AppearanceSync][CLIENT] ApplyAppearanceFromSync {DisplayName}: PlayerPawn invalide (probablement pas encore replique) — skip" );
			return;
		}

		var side = Networking.IsHost ? "HOST" : "CLIENT";
		Log.Info( $"[AppearanceSync][{side}] ApplyAppearanceFromSync {DisplayName}: skin={SavedSkinGroup}, head={SavedHeadIndex}, model={SavedModelPath}, isFemale={SavedIsFemale}, clothingJsonLen={SavedClothingJson?.Length ?? 0}" );

		var dresser = pawn.Components.Get<Dresser>( FindMode.EverythingInSelfAndChildren );
		var body = pawn.Components.Get<PlayerBody>( FindMode.EverythingInSelfAndChildren );
		if ( dresser == null )
		{
			Log.Warning( $"[AppearanceSync][{side}] ApplyAppearanceFromSync {DisplayName}: Dresser introuvable sur le pawn — skip" );
			return;
		}

		// Modele (male/female) AVANT tout — Dresser.Apply doit dresser le bon corps.
		if ( !string.IsNullOrEmpty( SavedModelPath ) && body?.Renderer.IsValid() == true )
		{
			var model = ResourceLibrary.Get<Model>( SavedModelPath );
			if ( model != null && body.Renderer.Model != model )
				body.Renderer.Model = model;
		}

		// Rebuild la liste de vetements depuis le JSON synced
		dresser.Clothing.Clear();
		if ( !string.IsNullOrEmpty( SavedClothingJson ) && SavedClothingJson != "[]" )
		{
			try
			{
				var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( SavedClothingJson );
				if ( paths != null )
				{
					foreach ( var path in paths )
					{
						var clothing = ResourceLibrary.Get<Clothing>( path );
						if ( clothing != null )
							dresser.Clothing.Add( new ClothingContainer.ClothingEntry { Clothing = clothing } );
					}
				}
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[ApplyAppearanceFromSync] JSON parse error pour {DisplayName}: {ex.Message}" );
			}
		}

		_ = ApplyDresserFromSyncAsync( dresser, body );
	}

	private async System.Threading.Tasks.Task ApplyDresserFromSyncAsync( Dresser dresser, PlayerBody body )
	{
		try
		{
			var side = Networking.IsHost ? "HOST" : "CLIENT";
			if ( PlayerBody.DebugMorphLogs )
				Log.Info( $"[Morphs][SYNC][{side}] {DisplayName} ApplyDresserFromSyncAsync START — morphsJsonLen={SavedMorphsJson?.Length ?? 0}, model={SavedModelPath}" );

			body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );
			await dresser.Apply();
			await GameTask.DelayRealtime( 1 );
			body?.RestoreAppearance( SavedSkinGroup, SavedHeadIndex, SavedMorphsJson );
			OpenFramework.Inventory.ClothingEquipment.RestoreSkinOnDresser( dresser, body );
			// Reapplique la teinte cheveux/barbe APRES le dresser (sinon les
			// renderers de cheveux ne sont pas encore montes / leur tint
			// est ecrase par defaut a chaque Apply).
			body?.ApplyHairColor( SavedHairColor, SavedBeardColor );

			if ( PlayerBody.DebugMorphLogs )
				Log.Info( $"[Morphs][SYNC][{side}] {DisplayName} ApplyDresserFromSyncAsync END" );
		}
		catch ( Exception ex )
		{
			Log.Error( $"[ApplyDresserFromSyncAsync] {ex.Message}" );
		}
	}

	/// <summary>
	/// The main PlayerPawn of this player if one exists, will not change when the player possesses gadgets etc. (synced)
	/// </summary>
	[Sync( SyncFlags.FromHost )] public PlayerPawn PlayerPawn { get; set; }

	/// <summary>
	/// The pawn this player is currently in possession of (synced - unless the pawn is not networked)
	/// </summary>
	[Sync] public Pawn Pawn { get; set; }

	[Property] public ClientData Data { get; set; }

	[Sync( SyncFlags.FromHost )] public List<Client> MutedPlayers { get; set; } = new(); 
	[Sync( SyncFlags.FromHost )] public bool IsChatMuted { get; set; } = false; 
	[Sync( SyncFlags.FromHost )] public bool IsGlobalVocalMuted { get; set; } = false; 
	[Sync( SyncFlags.FromHost )] public bool MuteIndefinite { get; set; } = false;

	// ───────────── General ─────────────
	[Sync( SyncFlags.FromHost )] public float JobSwitchEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float RespawnEndTime { get; set; }
	// Duree totale du timer de respawn courant — varie selon presence EMS au moment de la mort.
	// Sert a l'UI pour afficher une barre de progression coherente (RespawnDelay sans EMS, EMSWaitDelay avec).
	[Sync( SyncFlags.FromHost )] public float RespawnTotalDelay { get; set; }
	[Sync( SyncFlags.FromHost )] public float ChatEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float UseEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float UntilUnmuteEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float StealCooldownEndTime { get; set; }

	// ───────────── Police / Law ─────────────
	[Sync( SyncFlags.FromHost )] public float ArrestEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float FineEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float JailEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float CuffEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float SearchEndTime { get; set; }

	// ───────────── Crime ─────────────
	[Sync( SyncFlags.FromHost )] public float StealEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float LockpickEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float HackEndTime { get; set; }

	// ───────────── Medical ─────────────
	[Sync( SyncFlags.FromHost )] public float ReviveEndTime { get; set; }
	[Sync( SyncFlags.FromHost )] public float HealEndTime { get; set; }

	// ───────────── Utility Methods ─────────────

	/// <summary>
	/// Helper pour savoir s'il reste du temps sur un cooldown spécifique côté client.
	/// </summary>
	public float GetRemaining( float endTime )
	{
		return MathF.Max( 0f, endTime - Time.Now );
	}

	public async void HostInit()
	{
		SteamId = Connection.SteamId;
		SteamName = Connection.DisplayName;

		Log.Info( $"[Client] HostInit: {SteamName} initialized, waiting for character selection before spawn" );
	}

	/// <summary>
	/// Spawn le pawn du joueur. Appelé par le serveur quand le joueur sélectionne un personnage.
	/// </summary>
	public void SpawnPawn()
	{
		if ( PlayerPawn.IsValid() )
		{
			Log.Info( $"[Client] SpawnPawn: pawn already exists for {SteamName}, skipping" );
			return;
		}

		Log.Info( $"[Client] SpawnPawn: spawning pawn for {SteamName}" );
		Respawn( true, isInitialSpawn: true );
		Log.Info( $"[Client] SpawnPawn: done, PlayerPawn={PlayerPawn != null}" );
	}

	[Rpc.Owner]
	public void ClientInit()
	{
		Log.Info( $"[Client] ClientInit → setting Local (SteamId: {SteamId})" );
		Local = this;

		// Force le bridge à se réenregistrer après reconnexion (ownership reassignée)
		var bridge = GameObject.GetComponentInChildren<PlayerApiBridge>();
		if ( bridge != null )
		{
			Log.Info( $"[Client] ClientInit → forcing PlayerApiBridge.Local (was null: {PlayerApiBridge.Local == null})" );
			PlayerApiBridge.Local = bridge;
		}
		else
		{
			Log.Warning( $"[Client] ClientInit → PlayerApiBridge NOT found on GameObject!" );
		}
	}

	/*private void InitialiseData()
	{
		if ( !Networking.IsHost ) return;

		var table = DatabaseManager.Get<UserTable>();
		if ( table == null ) return;

		var steamId = Connection.SteamId;
		var now = DateTime.UtcNow;
		var constants = Constants.Instance;

		var row = table.GetAllRows().FirstOrDefault( x => x.SteamId == steamId );

		if ( row == null )
		{
			row = new UserDTO
			{
				Id = Guid.NewGuid(),
				SteamId = steamId,
				Username = Connection?.DisplayName ?? $"Player_{steamId}",
				FirstJoined = now,
				LastLogin = now,
				LastActive = now,
				IsOnline = true,

				// Économie par défaut
				Bank = constants?.DefaultBank ?? 0,
				Money = constants?.DefaultCash ?? 0,

				// Statuts
				IsBanned = false,
				BanReason = string.Empty,
				IPAddress = Connection?.Address ?? string.Empty,

				// Progression
				PlayTime = TimeSpan.Zero,
				LastXPChange = now,
				SessionXP = 0,
				BankTransferHistory = new(),

				// Job par défaut (identifiant stable, pas DisplayName)
				JobId = "citizen",
				JobGrade = "",

				// Toujours non null
				Warnings = new List<string>(),
			};

			table.InsertRow( row );
		}
		else
		{
			// Mise à jour connexion
			row.IsOnline = true;
			row.LastLogin = now;
			row.LastActive = now;

			// Facultatif: maj Username/IP si tu veux suivre les changements
			row.Username = Connection?.DisplayName ?? row.Username;
			row.IPAddress = Connection?.Address ?? row.IPAddress;
		}

		Data = row;
	}*/

	public void Kick( string reason = "No reason" )
	{
		if ( PlayerPawn.IsValid() )
		{
			PlayerPawn.GameObject.Destroy();
		}

		GameObject.Destroy();

		// Kick the client
		Network.Owner.Kick( reason );
	}

	public static void OnPossess( Pawn pawn )
	{
		if ( !pawn.IsValid() )
		{
			Log.Warning( "Tried to possess an invalid pawn." );
			return;
		}

		if ( !Local.IsValid() )
		{
			Log.Warning( "Tried to possess a pawn but we don't have a local Client" );
			return;
		}

		// called from Pawn when one is newly possessed, update Local and Viewer, invoke RPCs for observers

		Local.Pawn = pawn;

		if ( pawn.Network.Active )
		{
			Local.OnNetPossessed();
		}

		if ( !pawn.Client.IsValid() )
		{
			Log.Warning( $"Attempted to possess pawn, but pawn '{pawn.DisplayName}' has no attached Client! Using Local." );
			Viewer = Local;
			return;
		}

		Viewer = pawn.Client;
	}

	// sync to other clients what this player is currently possessing
	// Sol: when we track observers we could drop this with an Rpc.FilterInclude?
	[Rpc.Broadcast]
	private void OnNetPossessed()
	{
		if ( IsViewer && IsProxy )
		{
			Possess();
		}
	}

	public void Possess()
	{
		if ( Pawn is null || IsLocalPlayer )
		{
			if ( PlayerPawn.IsValid() )
			{
				// Local player - always assume the controller
				PlayerPawn.Possess();
			}
		}
		else
		{
			// A remote player is possessing this player (spectating)
			// So enter the latest known pawn this player has possessed
			Pawn.Possess();
		}
	}

	public bool IsAdmin => IsSteamIdAdmin( SteamId );

	/// <summary>
	/// Liste centralisee des SteamId admin. Utilisable autant cote client (gating UI)
	/// que cote serveur (validation d'autorite dans les RPC.Host).
	/// TODO: charger depuis un fichier de config externe (ex: Config/admins.json) ou ConVar.
	/// Liste vide par defaut — chaque hebergeur ajoute ses propres SteamId admin.
	/// </summary>
	public static readonly HashSet<ulong> AdminSteamIds = new HashSet<ulong>();

	public static bool IsSteamIdAdmin( ulong steamId ) => AdminSteamIds.Contains( steamId );
}
