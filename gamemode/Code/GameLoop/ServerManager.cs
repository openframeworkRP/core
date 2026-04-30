using System.Threading.Tasks;
using Sandbox.Events;
using Sandbox.Network;
using OpenFramework;
using OpenFramework.Api;
using OpenFramework.ChatSystem;
using OpenFramework.Command;
using OpenFramework.GameLoop;
using OpenFramework.Systems.AntiCheat;
using static Facepunch.NotificationSystem;

namespace Facepunch;

/// <summary>
/// Handles basic game networking. Creates Client prefabs when people join.
/// </summary>
public sealed class ServerManager : SingletonComponent<ServerManager>, Component.INetworkListener
{
	/// <summary>
	/// Which player prefab should we spawn?
	/// </summary>
	[Property] public GameObject ClientPrefab { get; set; }

	
	[ConVar("whitelist_mod", ConVarFlags.Server, Help = "Activer le mode whitelist sur le serveur")]
	public static bool WhitelistWasActivated { get; set;} = false;

	/// <summary>
	/// Quand true, seuls les SteamId admin (cf. Client.IsSteamIdAdmin) peuvent rejoindre le
	/// serveur. Les non-admins sont kick a la connexion. N'expulse pas les joueurs deja
	/// connectes — ne bloque que les nouvelles connexions.
	/// </summary>
	[ConVar("server_locked", ConVarFlags.Server, Help = "Verrouiller le serveur (seuls les admins peuvent rejoindre)")]
	public static bool IsServerLocked { get; set; } = false;

	// TODO: charger depuis un fichier de config externe (ex: Config/whitelist.json)
	// Liste vide par defaut — chaque hebergeur ajoute ses propres SteamId.
	public static List<string> WhitelistedSteamId = new List<string>();

	/// <summary>
	/// Is this game multiplayer? If not, we won't create a lobby.
	/// </summary>
	[Property] public bool IsMultiplayer { get; set; } = true;

	protected override async void OnStart()
	{
		if ( !IsMultiplayer )
		{
			OnActive( Connection.Local );
			return;
		}

		//
		// Create a lobby if we're not connected
		//
		if ( !Networking.IsActive )
		{
			Networking.CreateLobby( new LobbyConfig()
			{
				// ?
				MaxPlayers = 64
			} );
			
		}
	}

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;


		// timer pour les adverts de chat
		Timer.Host( "chat_adverts", Constants.Instance.ChatAdvertsDelay, () => SendChatAdvert(), true );

		// Garantit la presence du tracker de cleanup des props placés.
		OpenFramework.GameLoop.Rules.PlacedPropsCleanup.EnsureExists( Scene );
	}

	/// <summary>
	/// Envoi un advert de chat : choisit un message al�atoire et l'envoie � tous les clients.
	/// </summary>
	private void SendChatAdvert()
	{
		if ( !Networking.IsHost ) return;
		if ( !Constants.Instance.EnableChatAdverts ) return;
		if ( Constants.Instance.ChatAdverts == null || Constants.Instance.ChatAdverts.Count == 0 ) return;

		// choisir un message al�atoire
		var index = Game.Random.Int( 0, Constants.Instance.ChatAdverts.Count - 1 );
		var message = Constants.Instance.ChatAdverts[index];

		ChatUI.Receive( new ChatUI.ChatMessage()
		{
			HostMessage = true,
			Message = message,
			AuthorId = Connection.Host.Id
		} );
	}

	/// <summary>
	/// Tries to recycle a player state owned by this player (if they disconnected) or makes a new one.
	/// </summary>
	/// <param name="channel"></param>
	/// <returns></returns>
	private Client GetOrCreateClient( Connection channel = null )
	{
		var Clients = Scene.GetAllComponents<Client>();
		var possibleClient = Clients.FirstOrDefault( x =>
		{
			// A candidate player state has no owner.
			return x.Connection is null && x.SteamId == channel.SteamId;
		} );
		if ( possibleClient.IsValid() )
		{
			Log.Warning( $"[Reco:Join] Client existant retrouve pour {channel.SteamId} ({channel.DisplayName}), reutilisation." );
			return possibleClient;
		}

		Log.Info( $"[Reco:Join] Aucun Client existant pour {channel.SteamId} ({channel.DisplayName}), creation d'un nouveau." );

		if ( !ClientPrefab.IsValid() )
		{
			Log.Warning( "Could not spawn player as no ClientPrefab assigned." );
			return null;
		}

		var player = ClientPrefab.Clone();
		//player.BreakFromPrefab();
		player.Name = $"Client ({channel.DisplayName})";
		player.Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );

		var Client = player.GetComponent<Client>();
		if ( !Client.IsValid() )
			return null;

		return Client;
	}

	/// <summary>
	/// Called when a network connection becomes active
	/// </summary>
	/// <param name="channel"></param>
	public async void OnActive( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' is becoming active" );

		Log.Info( $"ApiComponent.Instance = {ApiComponent.Instance}" );

		// Check whitelist/ban EN PREMIER avec le channel.SteamId (Client.SteamId pas encore set)
		if ( !await CheckWhitelistAndBan( channel ) )
			return;

		// Spawn du Client APRÈS la vérif — le joueur doit être réseau-actif
		// avant de pouvoir recevoir des RPCs
		var client = GetOrCreateClient( channel );

		Log.Info( $"PlayerApiBridge.Instance = {PlayerApiBridge.Local}" );
		if ( !client.IsValid() )
			throw new Exception( $"Something went wrong when trying to create Client for {channel.DisplayName}" );

		OnPlayerJoined( client, channel );

		// Audit : ouvre une session côté API (fire-and-forget, ne bloque pas l'arrivée joueur).
		// Si l'API est down, on log un warning mais le join continue normalement.
		_ = ApiComponent.Instance?.LogSessionJoin( (ulong)channel.SteamId.Value, channel.DisplayName );

		// Petit délai pour laisser le network spawn se stabiliser
		await Task.DelayRealtimeSeconds( 0.5f );

		// Auth APRÈS que le joueur est spawné et connecté
		Log.Info( $"[Bridge] StartAuthentication appelé pour {channel.SteamId}" );
		var playerApiBridge = client.GameObject.GetComponentInChildren<PlayerApiBridge>();
		playerApiBridge.StartAuthentication();
		// TODO: Give les inventaires
	}

	private async Task<bool> CheckWhitelistAndBan( Connection channel )
	{
		if ( !Networking.IsHost ) return true;

		// L'hôte ne peut pas se bannir lui-même et démarre en parallèle de l'auth API —
		// on bypasse tous les checks pour éviter la race condition avec _serverToken vide.
		if ( channel.IsHost || Game.IsEditor ) return true;

		var steamId = channel.SteamId.Value.ToString();

		// Lock serveur : seuls les admins peuvent rejoindre. Verifie en premier pour
		// court-circuiter la whitelist API quand le serveur est ferme aux non-admins.
		if ( IsServerLocked && !Client.IsSteamIdAdmin( (ulong)channel.SteamId.Value ) )
		{
			Log.Info( $"[Lock] Connexion refusee a {channel.DisplayName} ({steamId}) : serveur verrouille (admins uniquement)." );
			channel.Kick( "Le serveur est actuellement verrouille (admins uniquement). Reessayez plus tard." );
			return false;
		}

		if ( WhitelistWasActivated )
		{
			Log.Info( steamId );
			try
			{
				var apiList = await ApiComponent.Instance.GetUserWhitelist();
				var whitelistRealList = WhitelistedSteamId
					.Union( apiList.Select( u => u.SteamId.ToString() ) )
					.ToList();

				if ( !whitelistRealList.Contains( steamId ) )
				{
					Log.Info( $"Kicking {channel.DisplayName}: Not Whitelisted" );
					channel.Kick( "You're not in the whitelist." );
					return false;
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[Whitelist] Vérification whitelist impossible (API non prête?) : {e.Message} — connexion refusée par sécurité." );
				channel.Kick( "Serveur en cours d'initialisation, réessaie dans quelques secondes." );
				return false;
			}
		}

		Log.Info( $"[CONNECTION]: Verification de l'état de bannissement de {channel.DisplayName}" );
		try
		{
			var banList = await ApiComponent.Instance.GetBanList();
			var user = banList?.FirstOrDefault( t => t.SteamId == steamId );
			if ( user != null )
			{
				Log.Info( $"[CONNECTION]: {channel.DisplayName} est un joueur bannis du serveur pour le motif suivant : {user.Reason} ," +
				          $" par l'administrateur comportant le steam id {user.FromAdminSteamId}" );
				channel.Kick( $"Vous avez été banni du serveur pour le motif suivant : {user.Reason}" +
				              $" Ce bannissement est révocable sur notre serveur discord : https://discord.gg/juuQU5vnKt ." +
				              $" Votre bannissement a été décidé par l''administrateur comportant ce steam id {user.FromAdminSteamId}" );
				return false;
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Whitelist] Vérification ban impossible (API non prête?) : {e.Message} — joueur accepté par défaut." );
		}

		return true;
	}

 
	protected override void OnDestroy()
	{
		if ( !Networking.IsHost ) return;

		Log.Info( "[ServerManager] Shutdown détecté — sauvegarde des positions de tous les joueurs connectés..." );

		var clients = Scene.GetAllComponents<Client>().ToList();
		foreach ( var cl in clients )
		{
			if ( !cl.IsValid() ) continue;
			Log.Info( $"[ServerManager] Sauvegarde shutdown pour {cl.DisplayName} (SteamId={cl.SteamId})" );
			cl.SavePositionAndDestroyPawn();
		}
	}

	void INetworkListener.OnDisconnected( Connection channel )
	{
		Log.Info( $"Player '{channel.DisplayName}' is disconnecting" );

		// Audit : ferme la session côté API. SessionId=null → l'API ferme la dernière
		// session active de ce SteamId (gère naturellement les reconnexions multiples).
		_ = ApiComponent.Instance?.LogSessionLeave( (ulong)channel.SteamId.Value, null );

		// ❌ Ne plus appeler ApiComponent.Instance?.RemoveToken() ici
		//    C'est PlayerPawn.OnDestroy qui s'en charge après avoir sauvegardé

		var cl = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.Connection == channel );
		if ( !cl.IsValid() )
		{
			Log.Warning( $"No Client found for {channel.DisplayName}" );
			return;
		}
		Scene.Dispatch( new PlayerDisconnectedEvent( cl ) );
		AntiCheatLogger.OnPlayerDisconnect( cl );

		// Sauvegarde l'inventaire + position AVANT de détruire le pawn
		// (évite la duplication quand un joueur drop puis déco avant l'auto-save périodique)
		cl.SavePositionAndDestroyPawn();
	}
	public void OnPlayerJoined( Client Client, Connection channel )
	{
		Scene.Dispatch( new PlayerConnectedEvent( Client ) );

		Sandbox.Services.Achievements.Unlock( "first_join" );
		// Either spawn over network, or claim ownership
		if ( !Client.Network.Active )
			Client.GameObject.NetworkSpawn( channel );
		else
			Client.Network.AssignOwnership( channel );

		Client.HostInit();
		Client.ClientInit();

		AntiCheatLogger.OnPlayerJoin( Client );

		Scene.Dispatch( new PlayerJoinedEvent( Client ) );
	}
}
