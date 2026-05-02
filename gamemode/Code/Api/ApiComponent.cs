using Sandbox;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OpenFramework.Api;

/// <summary>
/// Deux types de JWT :
///  - JWT joueur  : 1 par SteamId, auth via Steam token
///  - JWT serveur : 1 unique, auth via ConVar secret au démarrage
/// </summary>
public class ApiComponent : Component
{
    public static ApiComponent Instance { get; private set; }

    [Property] public string BaseUrl { get; set; } = "http://localhost:8443/api";

    /// <summary>
    /// URL de repli utilisée quand s&box bloque l'URL principale (ex: URL prod depuis l'éditeur).
    /// </summary>
    [Property] public string LocalFallbackUrl { get; set; } = "http://localhost:8443/api";

    /// <summary>
    /// Désactive tous les appels API. Peut être forcé manuellement ; jamais basculé automatiquement.
    /// </summary>
    [Property] public bool OfflineMode { get; set; } = false;

    // JWT joueurs
    private readonly Dictionary<ulong, string> _playerTokens = new();

    // JWT serveur — pour les endpoints sensibles (ATM, banque)
    private string _serverToken;
    public bool IsServerAuthenticated => !string.IsNullOrEmpty( _serverToken );

    [ConVar( "core-api_server_secret", Help = "Secret pour l'auth serveur API" )]
    public static string ServerSecret { get; set; } = "";

    [ConVar( "core-api_dev_bypass", Help = "Dev uniquement : saute la validation Facepunch si le token Steam est indisponible. Ne jamais activer en prod." )]
    public static bool DevBypass { get; set; } = false;

    [ConCmd("core-connect_server", ConVarFlags.Server)]
    public static void ConnectServer()
    {
	    Log.Info( "la variable secret est " +  ServerSecret);

	    _ = ApiComponent.Instance.AuthenticateServerAsync();
    }
    
    public Task<CharacterApi> GetActiveCharacter( ulong steamId )
	    => PlayerGet<CharacterApi>( steamId, "/Character/selected" );

    [ConCmd( "core-save_positions" , ConVarFlags.Server)]
    public static void SaveAllPositions()
    {
	    var players = Game.ActiveScene.GetAllComponents<PlayerPawn>();
	    foreach ( var player in players )
	    {
		    player.Destroy();
	    }
    }
    protected override void OnAwake()
    {
        if ( !Networking.IsHost ) return;
        Instance = this;

        if ( OfflineMode )
        {
            Log.Warning( "[API] OfflineMode forcé — aucun appel API ne sera effectué." );
            return;
        }

        // Auth serveur au démarrage — bascule automatiquement en OfflineMode si s&box bloque l'URL
        _ = AuthenticateServerAsync();
    }

    // ══════════════════════════════════════════════════════════════
    //  AUTH SERVEUR (au démarrage)
    // ══════════════════════════════════════════════════════════════

    private async Task AuthenticateServerAsync()
    {
        // Retry avec backoff exponentiel si le backend n'est pas encore joignable.
        // Si s&box bloque l'URL (ex: prod depuis l'éditeur), on bascule sur LocalFallbackUrl
        // et on repart depuis le début — pas besoin d'OfflineMode.
        var delays = new[] { 1f, 2f, 4f, 8f };
        for ( int attempt = 0; attempt <= delays.Length; attempt++ )
        {
            try
            {
                Log.Info( $"[API] Auth serveur... (tentative {attempt + 1}/{delays.Length + 1}, url: {BaseUrl})" );

                var body     = new { ServerSecret = ServerSecret };
                var response = await Http.RequestAsync(
                    $"{BaseUrl}/auth/server-login", "POST", Http.CreateJsonContent( body ) );

                if ( !response.IsSuccessStatusCode )
                {
                    Log.Error( $"[API] Auth serveur échouée : {response.StatusCode}" );
                    return;
                }

                var data = JsonSerializer.Deserialize<AuthResponse>(
                    await response.Content.ReadAsStringAsync(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

                _serverToken = data?.AccessToken;
                Log.Info( $"[API] Serveur authentifié ✓ ({BaseUrl})" );

                _ = CloseAllStaleSessions();
                RetriggerPlayerAuthForConnectedClients();
                return;
            }
            catch ( Exception e )
            {
                // s&box bloque l'URL (non-whitelistée) → basculer sur localhost et recommencer
                if ( e.Message.Contains( "is not allowed" ) )
                {
                    if ( BaseUrl != LocalFallbackUrl )
                    {
                        Log.Warning( $"[API] URL bloquée par s&box ({BaseUrl}) — basculement sur {LocalFallbackUrl}" );
                        BaseUrl = LocalFallbackUrl;
                        attempt = -1; // repart à 0 après l'incrément du for
                        continue;
                    }
                    // LocalFallbackUrl aussi bloquée — abandon
                    Log.Error( $"[API] {LocalFallbackUrl} aussi bloquée — auth abandonnée." );
                    return;
                }

                if ( attempt < delays.Length )
                {
                    Log.Warning( $"[API] Auth serveur indisponible ({e.Message}), retry dans {delays[attempt]}s..." );
                    await Task.Delay( (int)(delays[attempt] * 1000f) );
                }
                else
                {
                    Log.Error( $"[API] Auth serveur abandonnée après {delays.Length + 1} tentatives." );
                }
            }
        }
    }

    private void RetriggerPlayerAuthForConnectedClients()
    {
        var clients = Game.ActiveScene?.GetAllComponents<OpenFramework.Systems.Pawn.Client>();
        if ( clients == null ) return;

        foreach ( var cl in clients )
        {
            if ( !cl.IsValid() ) continue;
            if ( cl.Connection == null ) continue;
            if ( _playerTokens.ContainsKey( cl.SteamId ) ) continue;

            var bridge = cl.GameObject.GetComponentInChildren<OpenFramework.PlayerApiBridge>();
            if ( bridge == null ) continue;

            Log.Info( $"[API] Re-declenche auth joueur pour {cl.SteamId} apres auth serveur OK" );
            bridge.StartAuthentication();
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  AUTH JOUEUR (à la connexion de chaque joueur)
    // ══════════════════════════════════════════════════════════════

    public async Task<bool> AuthenticateWithSteamToken( ulong steamId, string steamToken )
    {
        try
        {
            Log.Info( $"[API] Auth joueur {steamId}..." );

            var body     = new { Id = steamId.ToString(), Token = steamToken };
            var response = await Http.RequestAsync(
                $"{BaseUrl}/auth/login", "POST", Http.CreateJsonContent( body ) );

            if ( !response.IsSuccessStatusCode )
            {
                Log.Error( $"[API] Auth joueur {steamId} échouée : {response.StatusCode}" );
                return false;
            }

            var data = JsonSerializer.Deserialize<AuthResponse>(
                await response.Content.ReadAsStringAsync(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

            if ( data?.AccessToken == null ) return false;

            _playerTokens[steamId] = data.AccessToken;
            Log.Info( $"[API] Joueur {steamId} authentifié ✓" );
            return true;
        }
        catch ( Exception e )
        {
            Log.Error( $"[API] Erreur auth joueur : {e.Message}" );
            return false;
        }
    }

    public bool IsAuthenticated( ulong steamId ) => _playerTokens.ContainsKey( steamId );

    public void RemoveToken( ulong steamId ) => _playerTokens.Remove( steamId );

    // ══════════════════════════════════════════════════════════════
    //  HELPERS
    // ══════════════════════════════════════════════════════════════

    private static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };

    private Dictionary<string, string> PlayerHeaders( ulong steamId )
    {
        if ( !_playerTokens.TryGetValue( steamId, out var t ) )
            throw new InvalidOperationException( $"Joueur {steamId} non authentifié !" );
        return new() { { "Authorization", $"Bearer {t}" } };
    }

    private Dictionary<string, string> ServerHeaders()
    {
        if ( string.IsNullOrEmpty( _serverToken ) )
            throw new InvalidOperationException( "Serveur non authentifié !" );
        return new() { { "Authorization", $"Bearer {_serverToken}" } };
    }

    private async Task<T> PlayerGet<T>( ulong steamId, string path ) where T : class
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "GET", null, PlayerHeaders( steamId ) );
        return r.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<T>( await r.Content.ReadAsStringAsync(), Opts )
            : null;
    }

    private async Task<T> PlayerPost<T>( ulong steamId, string path, object body ) where T : class
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "POST", Http.CreateJsonContent( body ), PlayerHeaders( steamId ) );
        return r.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<T>( await r.Content.ReadAsStringAsync(), Opts )
            : null;
    }

    private async Task PlayerPost( ulong steamId, string path, object body )
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "POST", Http.CreateJsonContent( body ), PlayerHeaders( steamId ) );
        if ( !r.IsSuccessStatusCode ) Log.Warning( $"[API] POST {path} → {r.StatusCode}" );
    }

    private async Task PlayerDelete( ulong steamId, string path )
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "DELETE", null, PlayerHeaders( steamId ) );
        if ( !r.IsSuccessStatusCode ) Log.Warning( $"[API] DELETE {path} → {r.StatusCode}" );
    }

    private async Task<T> ServerGet<T>( string path ) where T : class
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "GET", null, ServerHeaders() );
        return r.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<T>( await r.Content.ReadAsStringAsync(), Opts )
            : null;
    }

    private async Task<T> ServerPost<T>( string path, object body ) where T : class
    {
        var r = await Http.RequestAsync( $"{BaseUrl}{path}", "POST", Http.CreateJsonContent( body ), ServerHeaders() );
        return r.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<T>( await r.Content.ReadAsStringAsync(), Opts )
            : null;
    }

    // ══════════════════════════════════════════════════════════════
    //  ENDPOINTS JOUEUR
    // ══════════════════════════════════════════════════════════════

    public Task<List<CharacterApi>> GetCharacters( ulong steamId )
        => PlayerGet<List<CharacterApi>>( steamId, "/Character/all" );

    public Task<CharacterApi> CreateCharacter( ulong steamId, CharacterCreationDto dto )
        => PlayerPost<CharacterApi>( steamId, "/Character/create", dto );

    public Task SetActiveCharacter( ulong steamId, string characterId )
        => PlayerPost( steamId, $"/Character/{characterId}/select", new { } );

    public Task DeleteCharacter( ulong steamId, string characterId )
        => PlayerDelete( steamId, $"/Character/{characterId}/delete" );

    /// <summary>
    /// Persiste la coupe/barbe + couleurs choisies chez le coiffeur.
    /// Appele cote HOST depuis Client.RPC.BroadcastHairColor (apres ecriture des Saved* synced).
    /// </summary>
    public Task UpdateCharacterAppearance( ulong steamId, string characterId, AppearanceBody dto )
        => PlayerPost( steamId, $"/Character/{characterId}/appearance/update", dto );

    public Task UpdatePosition( ulong steamId, string characterId, Vector3 pos )
        => PlayerPost( steamId, $"/characters/{characterId}/positions/update",
            new { X = pos.x, Y = pos.y, Z = pos.z } );
	
    public Task UpdadteActualJob( ulong steamId, string characterId, string jobId )
		=> PlayerPost( steamId, $"/character/{characterId}/changeActualJob?newJobIdent={jobId}", null ); 
    
    
    public async Task<Vector3?> GetLastPosition( ulong steamId, string characterId )
    {
        var dto = await PlayerGet<PositionDto>( steamId, $"/characters/{characterId}/positions/lastposition" );
        return dto == null ? null : new Vector3( dto.X, dto.Y, dto.Z );
    }

    // ══════════════════════════════════════════════════════════════
//  ENDPOINTS INVENTAIRE (JWT joueur)
// ══════════════════════════════════════════════════════════════

    public Task<List<InventoryItemDto>> GetInventory( ulong steamId )
	    => PlayerGet<List<InventoryItemDto>>( steamId, $"/characters/actual/inventory/get" );

    public async Task AddInventoryItem( ulong steamId, InventoryItemDto item )
    {
        await PlayerPost( steamId, $"/characters/actual/inventory/add", item );
        // Audit : trace la persistance de l'item (utile pour reconstituer ce qu'on a en base
        // après un Save complet — combiné avec save_clear ça donne le snapshot stocké).
        if ( item != null )
            _ = LogInventoryTransfer( steamId, "add_persist", item.ItemGameId, item.Count,
                sourceType: "memory", targetType: "api_save",
                metadataJson: $"{{\"line\":{item.Line},\"col\":{item.Collum},\"mass\":{item.Mass}}}" );
    }

    public async Task DeleteInventoryItem( ulong steamId, InventoryItemDto item )
    {
        await PlayerPost( steamId, $"/characters/actual/inventory/delete", item );
        if ( item != null )
            _ = LogInventoryTransfer( steamId, "remove_persist", item.ItemGameId, item.Count,
                sourceType: "api_save", targetType: "void" );
    }

    public async Task ClearInventory( ulong steamId )
    {
        await PlayerPost( steamId, $"/characters/actual/inventory/clear", new { } );
        // Marque le début d'une fenêtre de save : tous les add_persist juste après
        // appartiennent au même snapshot.
        _ = LogInventoryTransfer( steamId, "save_clear", "", 0,
            sourceType: "api_save", targetType: "void" );
    }
    // ══════════════════════════════════════════════════════════════
    //  ENDPOINTS SERVEUR — ATM (JWT serveur uniquement)
    // ══════════════════════════════════════════════════════════════

    /// <summary>Récupère le compte bancaire lié à un personnage.</summary>
    public Task<BankAccountDto> GetBankAccount( string characterId )
	    => ServerGet<BankAccountDto>( $"/atm/account/{characterId}" );
 
// Récupère les transactions d'un compte (route corrigée)
    public Task<List<TransactionDto>> GetTransactions( string accountId )
	    => ServerGet<List<TransactionDto>>( $"/atm/transactions/{accountId}" );
 
// Dépôt ATM
    public Task<TransactionResultDto> AtmDeposit( string atmId, string accountId, string characterId, decimal amount )
	    => ServerPost<TransactionResultDto>( "/atm/deposit", new
	    {
		    AtmId           = atmId,
		    ToAccountId     = accountId,
		    InitiatorCharId = characterId,
		    Amount          = amount
	    } );
 
// Retrait ATM
    public Task<TransactionResultDto> AtmWithdrawal( string atmId, string accountId, string characterId, decimal amount )
	    => ServerPost<TransactionResultDto>( "/atm/withdrawal", new
	    {
		    AtmId           = atmId,
		    FromAccountId   = accountId,
		    InitiatorCharId = characterId,
		    Amount          = amount
	    } );

// Virement compte → compte (ToAccountNumber = numéro affiché, pas l'UUID)
    public Task<TransactionResultDto> AtmTransfer( string atmId, string fromAccountId, string toAccountNumber, string characterId, decimal amount, string comment )
	    => ServerPost<TransactionResultDto>( "/atm/transfer", new
	    {
		    AtmId           = atmId,
		    FromAccountId   = fromAccountId,
		    ToAccountNumber = toAccountNumber,
		    InitiatorCharId = characterId,
		    Amount          = amount,
		    Comment         = comment
	    } );

	// Salaire — credit serveur sur un compte (JWT GameServer uniquement, pas appelable par un client)
    public Task<TransactionResultDto> PaySalary( string toAccountId, decimal amount, string reason )
	    => ServerPost<TransactionResultDto>( "/atm/salary", new
	    {
		    ToAccountId = toAccountId,
		    Amount      = amount,
		    Reason      = reason
	    } );

    
    // Admin System

    public Task<List<UserBan>> GetBanList() 
	    => ServerGet<List<UserBan>>( $"/ban/getList"  );

    public Task<AdminResult> BanUser(string userSteamId, string reason, string fromAdminSteamId)
	    => ServerPost<AdminResult>( "ban", new AddUserBanDto()
	    {
		    UserSteamId = userSteamId,
		    Reason = reason,
		    AdminSteamId = fromAdminSteamId
	    });

    public Task<AdminResult> UnBanUser( string userSteamId, string reason, string fromAdminSteamId )
	    => ServerPost<AdminResult>( $"unban/{userSteamId}",
		    new RemoveUserBanDto() { AdminSteamId = fromAdminSteamId, Reason = reason } );

    public Task<AdminResult> AddPlayerInWhitelist(string userSteamId,  string fromAdminSteamId) 
	    => ServerPost<AdminResult>("whitelist",  new AddUserInWhitelistDto()
    {
	    AdminSteamId = fromAdminSteamId,
	    UserSteamId = userSteamId
    });
    
    public Task<AdminResult> RemoveWhiteListPlayer(string userSteamId,  string fromAdminSteamId) 
	    => ServerPost<AdminResult>($"whitelist/{userSteamId}/supp",  null);
    
    public Task<List<UserWhitelist>> GetUserWhitelist()
		=> ServerGet<List<UserWhitelist>>( $"/whitelist/getList" );

    // ══════════════════════════════════════════════════════════════
    //  AUDIT — sessions, chat, actions admin (JWT serveur)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Notifie l'API qu'un joueur vient de rejoindre. Renvoie l'Id de session
    /// qu'on stocke dans le Client pour pouvoir le passer au leave.
    /// Fire-and-forget côté caller : on ne bloque jamais le flow d'arrivée joueur.
    /// </summary>
    public async Task<Guid?> LogSessionJoin( ulong steamId, string displayName )
    {
        try
        {
            var dto = new { SteamId = steamId.ToString(), DisplayName = displayName ?? "" };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/session/join", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) { Log.Warning( $"[Audit] session/join → {r.StatusCode}" ); return null; }
            var body = JsonSerializer.Deserialize<SessionJoinResult>(
                await r.Content.ReadAsStringAsync(), Opts );
            return body?.Id;
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] session/join : {e.Message}" ); return null; }
    }

    /// <summary>
    /// Ferme côté API toutes les sessions restées ouvertes (LeftAt = null).
    /// Appelé une seule fois au boot du gamemode : si on démarre, c'est qu'un
    /// éventuel crash précédent a laissé des sessions fantômes en base.
    /// </summary>
    public async Task CloseAllStaleSessions()
    {
        try
        {
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/session/close-all-stale", "POST",
                Http.CreateJsonContent( new { } ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode )
                Log.Warning( $"[Audit] session/close-all-stale → {r.StatusCode}" );
            else
                Log.Info( "[Audit] Sessions fantômes fermées au boot." );
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] session/close-all-stale : {e.Message}" ); }
    }

    public async Task LogSessionLeave( ulong steamId, Guid? sessionId )
    {
        try
        {
            var dto = new { SteamId = steamId.ToString(), SessionId = sessionId };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/session/leave", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) Log.Warning( $"[Audit] session/leave → {r.StatusCode}" );
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] session/leave : {e.Message}" ); }
    }

    public async Task LogChat( ulong steamId, string authorName, string channel, string message, bool isCommand )
    {
        try
        {
            var dto = new
            {
                SteamId    = steamId.ToString(),
                AuthorName = authorName ?? "",
                Channel    = channel ?? "",
                Message    = message ?? "",
                IsCommand  = isCommand,
            };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/chat", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) Log.Warning( $"[Audit] chat → {r.StatusCode}" );
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] chat : {e.Message}" ); }
    }

    /// <summary>
    /// Journalise un transfert d'inventaire pour audit anti-dup. Best-effort, fire-and-forget.
    /// Action exemples : "add", "remove", "move", "drop", "pickup", "use", "save_full".
    /// </summary>
    public async Task LogInventoryTransfer(
        ulong actorSteamId, string action, string itemGameId, int count,
        string sourceType = "", string sourceId = null,
        string targetType = "", string targetId = null,
        string characterId = null, string metadataJson = null )
    {
        try
        {
            var dto = new
            {
                ActorSteamId = actorSteamId.ToString(),
                CharacterId  = characterId,
                Action       = action,
                ItemGameId   = itemGameId ?? "",
                Count        = count,
                SourceType   = sourceType ?? "",
                SourceId     = sourceId,
                TargetType   = targetType ?? "",
                TargetId     = targetId,
                MetadataJson = metadataJson,
            };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/inventory", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) Log.Warning( $"[Audit] inventory → {r.StatusCode}" );
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] inventory : {e.Message}" ); }
    }

    // ══════════════════════════════════════════════════════════════
    //  ADMIN COMMAND QUEUE (pull par le poller WebAdminDispatcher)
    // ══════════════════════════════════════════════════════════════

    public async Task<List<PendingAdminCommandDto>> FetchPendingAdminCommands()
    {
        try
        {
            var r = await Http.RequestAsync(
                $"{BaseUrl}/admin/command/pending?max=20", "GET", null, ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) { Log.Warning( $"[CmdQueue] pending → {r.StatusCode}" ); return new(); }
            return JsonSerializer.Deserialize<List<PendingAdminCommandDto>>(
                await r.Content.ReadAsStringAsync(), Opts ) ?? new();
        }
        catch ( Exception e ) { Log.Warning( $"[CmdQueue] pending : {e.Message}" ); return new(); }
    }

    public async Task<List<string>> FetchGameAdmins()
    {
        try
        {
            var r = await Http.RequestAsync(
                $"{BaseUrl}/admin/game-admins", "GET", null, ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) { Log.Warning( $"[GameAdmins] GET → {r.StatusCode}" ); return null; }
            var dto = JsonSerializer.Deserialize<GameAdminsListDto>(
                await r.Content.ReadAsStringAsync(), Opts );
            return dto?.SteamIds != null ? new List<string>( dto.SteamIds ) : null;
        }
        catch ( Exception e ) { Log.Warning( $"[GameAdmins] fetch : {e.Message}" ); return null; }
    }

    public async Task ReportAdminCommandResult( Guid id, bool success, string result )
    {
        try
        {
            var dto = new { Success = success, Result = result ?? "" };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/admin/command/{id}/result", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) Log.Warning( $"[CmdQueue] result → {r.StatusCode}" );
        }
        catch ( Exception e ) { Log.Warning( $"[CmdQueue] result : {e.Message}" ); }
    }

    public async Task LogAdminAction( string adminSteamId, string action, string targetSteamId = null, string reason = null, string source = "ingame" )
    {
        try
        {
            var dto = new
            {
                AdminSteamId  = adminSteamId,
                Action        = action,
                TargetSteamId = targetSteamId,
                Reason        = reason,
                Source        = source,
            };
            var r = await Http.RequestAsync(
                $"{BaseUrl}/events/admin-action", "POST",
                Http.CreateJsonContent( dto ), ServerHeaders() );
            if ( !r.IsSuccessStatusCode ) Log.Warning( $"[Audit] admin-action → {r.StatusCode}" );
        }
        catch ( Exception e ) { Log.Warning( $"[Audit] admin-action : {e.Message}" ); }
    }
}

internal class SessionJoinResult
{
    public Guid Id { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class PendingAdminCommandDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string RequestedByAdminSteamId { get; set; }
    public string Command { get; set; }
    public string TargetSteamId { get; set; }
    public string ArgsJson { get; set; }
    public string Status { get; set; }
}

// ── DTOs ────────────────────────────────────────────────────────────────────

public class UserWhitelist
{
	public string Id { get; set; }
	public string SteamId { get; set; }
	public string FromAdminSteamId { get; set; }
}


public class UserBan
{
	public string Id { get; set; }
	public string SteamId { get; set; }
	public string Reason { get; set; }
	public string FromAdminSteamId { get; set; }
}
public class AddUserBanDto
{
	public string UserSteamId { get; set; } = "";
	public string Reason { get; set; } = "";
	public string AdminSteamId { get; set; } = "";
}

public class RemoveUserBanDto
{
	public string Reason { get; set; } = "";
	public string AdminSteamId { get; set; } = "";
}

public class AddUserInWhitelistDto
{
	public string UserSteamId { get; set; } = "";
	public string AdminSteamId { get; set; } = "";
}

public class AddWarningDto
{
	public string UserSteamId { get; set; } = "";
	public string Reason { get; set; } = "";
	public string AdminSteamId { get; set; } = "";
}


public class InventoryItemDto
{
	public string  Id          { get; set; }
	public string  InventoryId { get; set; }
	public string  ItemGameId  { get; set; }
	public float   Mass        { get; set; }
	public int     Count       { get; set; }
	public Dictionary<string, string> Metadata { get; set; } = new();
	public int     Line        { get; set; }
	public int     Collum      { get; set; }
}
public class BankAccountDto
{
    public string  Id            { get; set; }
    public string AccountNumber { get; set; }
    public string  CharacterId   { get; set; }
    public decimal Balance       { get; set; }  // en euros, converti depuis cents côté API
    public bool    IsActive      { get; set; }
}

public class TransactionDto
{
    public string   Id                { get; set; }
    public int      Type              { get; set; }
    public string   FromAccountId     { get; set; }
    public string   ToAccountId       { get; set; }
    public string   FromAccountNumber { get; set; }  // null si tx externe (depot cash, salaire admin)
    public string   ToAccountNumber   { get; set; }  // null si tx externe (retrait cash)
    public decimal  Amount            { get; set; }
    public string   Comment           { get; set; }
    public DateTime CreatedAt         { get; set; }
}

public class TransactionResultDto
{
    public bool   Success { get; set; }
    public string Error   { get; set; }
    public string TxId    { get; set; }
}

public class AdminResult
{
	public bool Success { get; set; }
}

// ── DTOs ────────────────────────────────────────────────────────

public enum Gender  { Male, Female }
public enum Country { France, Germany }
public enum ColorBody {Dark, Light}

public class AuthResponse
{
    [JsonPropertyName( "access_token" )]
    public string AccessToken { get; set; }
}

public class AppearanceBody
{
    public Gender    Gender    { get; set; }
    public ColorBody SkinTone  { get; set; }
    public string    Morphs    { get; set; } = "{}";
    public string    Clothing  { get; set; } = "[]";
    public string    HairStyle  { get; set; } = "";
    public string    BeardStyle { get; set; } = "";
    public string    HairColor  { get; set; } = "#3a2a1c";
    public string    BeardColor { get; set; } = "#3a2a1c";
}

public class CharacterApi
{
    public string   Id         { get; set; }
    public string   OwnerId    { get; set; }
    public string   FirstName  { get; set; }
    public string   LastName   { get; set; }
    public int      Age        { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country  Origin     { get; set; }
    public float    Height     { get; set; }
    public float    Weight     { get; set; }
    public string   Occupation { get; set; }
    public bool     IsActive   { get; set; }
    public AppearanceBody Appearance { get; set; } = new();

    // ── Compat — délèguent vers Appearance pour ne pas casser l'existant ──

    [JsonIgnore] public Gender    Gender    => Appearance?.Gender    ?? Gender.Male;
    [JsonIgnore] public ColorBody ColorBody => Appearance?.SkinTone  ?? ColorBody.Light;
    [JsonIgnore] public string    ActualJob => Occupation             ?? "";
    [JsonIgnore] public string    HairColor  => Appearance?.HairColor  ?? "#3a2a1c";
    [JsonIgnore] public string    BeardColor => Appearance?.BeardColor ?? "#3a2a1c";
    [JsonIgnore] public string    HairStyle  => Appearance?.HairStyle  ?? "";
    [JsonIgnore] public string    BeardStyle => Appearance?.BeardStyle ?? "";

    // Morphs — lus depuis le JSON dict de Appearance.Morphs
    [JsonIgnore] public float BrowDown      => GetMorph( "BrowDown" );
    [JsonIgnore] public float BrowInnerUp   => GetMorph( "BrowInnerUp" );
    [JsonIgnore] public float BrowOuterUp   => GetMorph( "BrowOuterUp" );
    [JsonIgnore] public float EyesLookDown  => GetMorph( "EyesLookDown" );
    [JsonIgnore] public float EyesLookIn    => GetMorph( "EyesLookIn" );
    [JsonIgnore] public float EyesLookOut   => GetMorph( "EyesLookOut" );
    [JsonIgnore] public float EyesLookUp    => GetMorph( "EyesLookUp" );
    [JsonIgnore] public float EyesSquint    => GetMorph( "EyesSquint" );
    [JsonIgnore] public float EyesWide      => GetMorph( "EyesWide" );
    [JsonIgnore] public float CheekPuff     => GetMorph( "CheekPuff" );
    [JsonIgnore] public float CheekSquint   => GetMorph( "CheekSquint" );
    [JsonIgnore] public float NoseSneer     => GetMorph( "NoseSneer" );
    [JsonIgnore] public float JawForward    => GetMorph( "JawForward" );
    [JsonIgnore] public float JawLeft       => GetMorph( "JawLeft" );
    [JsonIgnore] public float JawRight      => GetMorph( "JawRight" );
    [JsonIgnore] public float MouthDimple   => GetMorph( "MouthDimple" );
    [JsonIgnore] public float MouthRollUpper => GetMorph( "MouthRollUpper" );
    [JsonIgnore] public float MouthStretch  => GetMorph( "MouthStretch" );

    private float GetMorph( string key )
    {
        var json = Appearance?.Morphs;
        if ( string.IsNullOrEmpty( json ) || json == "{}" ) return 0f;
        try
        {
            var d = JsonSerializer.Deserialize<Dictionary<string, float>>( json );
            return d != null && d.TryGetValue( key, out var v ) ? v : 0f;
        }
        catch { return 0f; }
    }
}

public class CharacterCreationDto
{
    public string   FirstName  { get; set; }
    public string   LastName   { get; set; }
    public int      Age        { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country  Origin     { get; set; }
    public float    Height     { get; set; } = 1.75f;
    public float    Weight     { get; set; } = 70f;
    public string   Occupation { get; set; }
    public AppearanceBody Appearance { get; set; } = new();
}

public class PositionDto
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

internal class GameAdminsListDto
{
    [JsonPropertyName( "steamIds" )]
    public string[] SteamIds { get; set; }
}
