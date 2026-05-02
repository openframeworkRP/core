using Sandbox;
using OpenFramework.Api;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems.Pawn;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenFramework;

/// <summary>
/// Poller qui pull périodiquement les commandes admin déposées par le panel
/// web et les exécute côté gamemode (host only). Permet à un staff de faire
/// kick/ban/give/heal depuis le site sans être connecté en jeu.
///
/// Architecture : voir AdminCommandController côté API. La queue est en BDD,
/// chaque commande passe par les états pending → processing → processed/failed.
/// Si le gamemode plante mid-exec, l'API reset les processing > 30s en pending.
///
/// Ajouter ce composant à la scène (à côté d'ApiComponent) pour activer.
/// </summary>
public class WebAdminDispatcher : Component
{
    [Property] public float PollIntervalSeconds { get; set; } = 5f;

    /// <summary>
    /// Intervalle (secondes) entre deux synchronisations de la liste d'admins jeu depuis core-api.
    /// </summary>
    [Property] public float AdminSyncIntervalSeconds { get; set; } = 60f;

    private TimeSince _timeSinceLastPoll = 0;
    private TimeSince _timeSinceAdminSync = 999f; // force une sync immédiate au démarrage
    private bool _busy;

    protected override void OnAwake()
    {
        if ( !Networking.IsHost )
        {
            Enabled = false;
            return;
        }
        Log.Info( "[WebAdmin] WebAdminDispatcher démarré (poll toutes les " + PollIntervalSeconds + "s)" );
    }

    protected override void OnUpdate()
    {
        if ( !Networking.IsHost ) return;
        if ( ApiComponent.Instance == null || !ApiComponent.Instance.IsServerAuthenticated ) return;

        // Sync liste admins depuis core-api
        if ( _timeSinceAdminSync >= AdminSyncIntervalSeconds )
        {
            _timeSinceAdminSync = 0;
            _ = SyncAdminList();
        }

        // Poll commandes admin en attente
        if ( _busy ) return;
        if ( _timeSinceLastPoll < PollIntervalSeconds ) return;

        _timeSinceLastPoll = 0;
        _busy = true;
        _ = PollAndDispatch();
    }

    private async Task PollAndDispatch()
    {
        try
        {
            var commands = await ApiComponent.Instance.FetchPendingAdminCommands();
            if ( commands.Count == 0 ) return;

            Log.Info( $"[WebAdmin] {commands.Count} commande(s) à exécuter" );
            foreach ( var cmd in commands )
            {
                var (ok, msg) = await ExecuteOne( cmd );
                await ApiComponent.Instance.ReportAdminCommandResult( cmd.Id, ok, msg );
            }
        }
        finally { _busy = false; }
    }

    private async Task<(bool ok, string msg)> ExecuteOne( PendingAdminCommandDto cmd )
    {
        try
        {
            // Résout la cible si fournie
            Client target = null;
            if ( !string.IsNullOrEmpty( cmd.TargetSteamId ) && ulong.TryParse( cmd.TargetSteamId, out var sid ) )
            {
                target = Scene.GetAllComponents<Client>().FirstOrDefault( c => c.IsValid() && c.SteamId == sid );
            }

            var args = ParseArgs( cmd.ArgsJson );

            var adminLabel = $"Admin web ({cmd.RequestedByAdminSteamId})";

            switch ( cmd.Command )
            {
                case "kick":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var reason = GetString( args, "reason", "Kicked by web admin" );
                    Command.Commands.WebAdminKick( target, reason );
                    return (true, $"Kick {target.DisplayName} : {reason}");
                }

                case "givemoney":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var amount = GetInt( args, "amount", 0 );
                    if ( amount == 0 ) return (false, "Montant requis (>0 ou <0)");
                    Command.Commands.WebAdminGiveMoney( target, amount, adminLabel );
                    return (true, $"GiveMoney {target.DisplayName} {amount}");
                }

                case "givebank":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var amount = GetInt( args, "amount", 0 );
                    if ( amount == 0 ) return (false, "Montant requis (>0 ou <0)");
                    Command.Commands.WebAdminGiveBankMoney( target, amount, adminLabel );
                    return (true, $"GiveBankMoney {target.DisplayName} {amount}");
                }

                case "heal":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var amount = GetFloat( args, "amount", -1f );
                    var ok = Command.Commands.WebAdminHealPlayer( target, amount );
                    return (ok, ok ? $"Heal {target.DisplayName} {(amount < 0 ? "max" : amount.ToString())}" : "Cible invalide ou pawn absent");
                }

                case "giveitem":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var itemId   = GetString( args, "itemid", "" );
                    var quantity = GetInt( args, "quantity", 1 );
                    if ( string.IsNullOrEmpty( itemId ) ) return (false, "itemid requis");
                    var ok = Command.Commands.WebAdminGiveItem( target, itemId, quantity, adminLabel );
                    return (ok, ok ? $"GiveItem {target.DisplayName} {itemId} ×{quantity}" : "Item ou container invalide");
                }

                case "ban":
                {
                    if ( target == null ) return (false, "Joueur non connecté");
                    var duration = GetInt( args, "duration", 0 );
                    var reason   = GetString( args, "reason", "Banned by web admin" );
                    var ok = await Command.Commands.WebAdminBan( target, duration, reason, cmd.RequestedByAdminSteamId );
                    return (ok, ok ? $"Ban {target.DisplayName} ({duration}min) : {reason}" : "Ban appliqué (kick) mais l'API a échoué");
                }

                // Notif post-DB : un personnage a été renommé via le panel web.
                // La DB est déjà à jour. Si le joueur propriétaire est connecté
                // et joue justement ce perso, on force un refresh de son nom RP
                // affiché. Sinon on confirme simplement l'ack.
                case "character_update":
                {
                    var characterId = GetString( args, "characterId", "" );
                    if ( string.IsNullOrEmpty( characterId ) ) return (false, "characterId manquant");
                    if ( target != null )
                    {
                        Command.Commands.WebAdminRefreshCharacter( target, characterId );
                        return (true, $"Refresh perso {characterId} sur {target.DisplayName}");
                    }
                    return (true, "Joueur hors-ligne — DB déjà à jour");
                }

                // Notif post-DB : un personnage a été supprimé via le panel web.
                // Si le joueur propriétaire est connecté avec ce perso actif,
                // on le kick proprement (sa session pointe sur un perso qui
                // n'existe plus en DB). Sinon, simple ack.
                case "character_delete":
                {
                    var characterId = GetString( args, "characterId", "" );
                    if ( string.IsNullOrEmpty( characterId ) ) return (false, "characterId manquant");
                    if ( target != null )
                    {
                        var kicked = Command.Commands.WebAdminKickIfPlayingCharacter( target, characterId, "Personnage supprimé par un admin" );
                        return (true, kicked
                            ? $"Joueur {target.DisplayName} kické (perso actif supprimé)"
                            : $"Joueur {target.DisplayName} connecté mais ne jouait pas ce perso");
                    }
                    return (true, "Joueur hors-ligne — DB déjà à jour");
                }

                default:
                    return (false, $"Commande inconnue : {cmd.Command}");
            }
        }
        catch ( Exception e )
        {
            Log.Warning( $"[WebAdmin] Erreur exec '{cmd.Command}' : {e.Message}" );
            return (false, $"Exception : {e.Message}");
        }
    }

    // ── Sync liste admins depuis core-api ────────────────────────────────────

    private async Task SyncAdminList()
    {
        try
        {
            var steamIds = await ApiComponent.Instance.FetchGameAdmins();
            if ( steamIds == null ) return;

            Client.AdminSteamIds.Clear();
            foreach ( var sid in steamIds )
            {
                if ( ulong.TryParse( sid, out var id ) )
                    Client.AdminSteamIds.Add( id );
            }

            Log.Info( $"[WebAdmin] Admins jeu synchronisés : {Client.AdminSteamIds.Count} admin(s)" );
        }
        catch ( Exception e )
        {
            Log.Warning( $"[WebAdmin] Erreur sync admins : {e.Message}" );
        }
    }

    // Helpers de parsing JSON tolérants
    private static System.Collections.Generic.Dictionary<string, JsonElement> ParseArgs( string json )
    {
        if ( string.IsNullOrWhiteSpace( json ) ) return new();
        try { return JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, JsonElement>>( json ) ?? new(); }
        catch { return new(); }
    }

    private static string GetString( System.Collections.Generic.Dictionary<string, JsonElement> args, string key, string def )
    {
        if ( !args.TryGetValue( key, out var el ) ) return def;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
    }

    private static int GetInt( System.Collections.Generic.Dictionary<string, JsonElement> args, string key, int def )
    {
        if ( !args.TryGetValue( key, out var el ) ) return def;
        if ( el.ValueKind == JsonValueKind.Number && el.TryGetInt32( out var i ) ) return i;
        if ( el.ValueKind == JsonValueKind.String && int.TryParse( el.GetString(), out var s ) ) return s;
        return def;
    }

    private static float GetFloat( System.Collections.Generic.Dictionary<string, JsonElement> args, string key, float def )
    {
        if ( !args.TryGetValue( key, out var el ) ) return def;
        if ( el.ValueKind == JsonValueKind.Number && el.TryGetSingle( out var f ) ) return f;
        if ( el.ValueKind == JsonValueKind.String && float.TryParse( el.GetString(), out var s ) ) return s;
        return def;
    }
}
