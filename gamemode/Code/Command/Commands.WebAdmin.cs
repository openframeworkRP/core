using Sandbox;
using Sandbox.Diagnostics;
using OpenFramework.Api;
using OpenFramework.ChatSystem;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

/// <summary>
/// Variantes des commandes admin invocables côté serveur SANS caller (pas de
/// Rpc.Caller). Utilisées par WebAdminDispatcher pour exécuter des commandes
/// déposées par le panel web. Pas de check IsAdmin ici — l'autorisation est
/// faite en amont au niveau de l'API (le backend Node ne queue que pour des
/// utilisateurs authentifiés en role editor+).
///
/// Garde l'admin SteamId en paramètre pour les notifications utilisateur
/// ("Vous avez reçu 100€ de la part de [SteamId]") et l'audit log.
/// </summary>
public static partial class Commands
{
    public static void WebAdminGiveMoney( Client target, int amount, string adminLabel )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return;
        MoneySystem.Add( target, amount );
        target.Notify( NotificationType.Success, $"Vous avez reçu {amount}€ de la part de {adminLabel}." );
    }

    public static void WebAdminGiveBankMoney( Client target, int amount, string adminLabel )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return;
        target.Data.Bank += amount;
        target.Notify( NotificationType.Success, $"Vous avez reçu {amount}€ sur votre compte bancaire de la part de {adminLabel}." );
    }

    public static async System.Threading.Tasks.Task<bool> WebAdminBan( Client target, int duration, string reason, string adminSteamId )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return false;

        var result = await ApiComponent.Instance.BanUser( target.SteamId.ToString(), reason, adminSteamId );
        // On kick même si l'API a échoué — l'admin a explicitement demandé un ban
        target.Connection?.Kick( $"Banned: {reason}\n Please consult our website: https://github.com/openframeworkRP" );
        return result?.Success ?? false;
    }

    public static void WebAdminKick( Client target, string reason )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return;
        if ( target.Connection?.IsHost == true ) return; // ne kick pas l'host

        ChatUI.Receive( new ChatUI.ChatMessage()
        {
            HostMessage = true,
            Message = $"{target.DisplayName} has been kicked from the server.({reason})",
        } );
        target.Connection?.Kick( reason );
    }

    public static bool WebAdminGiveItem( Client target, string itemId, int quantity, string adminLabel )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() || target.PlayerPawn == null ) return false;

        var metadata = ItemMetadata.All.FirstOrDefault( x =>
            x.ResourceName.Equals( itemId, System.StringComparison.OrdinalIgnoreCase ) );
        if ( metadata == null ) return false;

        var container = target.PlayerPawn.GameObject.Components
            .Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
        if ( container == null || !container.IsValid() ) return false;

        InventoryContainer.Add( container, itemId, quantity );

        target.Notify( NotificationType.Success,
            $"Vous avez reçu {quantity}× <font color='green'>{metadata.Name}</font> de la part de {adminLabel}." );
        return true;
    }

    /// <summary>
    /// Notifie un joueur que son personnage RP vient d'être renommé via le
    /// panel web admin. La DB est déjà à jour (PATCH /api/admin/character/{id}).
    /// On notifie côté UI seulement si le perso renommé est bien celui qu'il
    /// joue actuellement.
    /// </summary>
    public static void WebAdminRefreshCharacter( Client target, string characterId )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return;

        var active = PlayerApiBridge.GetActiveCharacter( target.SteamId );
        if ( active != characterId ) return; // pas le perso actif, rien à rafraîchir

        target.Notify( NotificationType.Info,
            "Votre identité RP vient d'être modifiée par un admin. Reconnectez-vous pour appliquer le changement complet." );
    }

    /// <summary>
    /// Si le joueur est en train de jouer le personnage donné, le kick proprement.
    /// Sinon ne fait rien. Renvoie true si un kick a été déclenché.
    /// </summary>
    public static bool WebAdminKickIfPlayingCharacter( Client target, string characterId, string reason )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() ) return false;
        if ( target.Connection?.IsHost == true ) return false; // ne kick pas l'host

        var active = PlayerApiBridge.GetActiveCharacter( target.SteamId );
        if ( active != characterId ) return false;

        target.Connection?.Kick( reason );
        return true;
    }

    public static bool WebAdminHealPlayer( Client target, float amount )
    {
        Assert.True( Networking.IsHost );
        if ( !target.IsValid() || target.PlayerPawn == null ) return false;

        var healthComp = target.PlayerPawn.HealthComponent;
        if ( !healthComp.IsValid() ) return false;

        if ( amount <= 0 )
        {
            // Heal complet
            healthComp.Health = healthComp.MaxHealth;
            if ( target.Data != null )
            {
                target.Data.Hunger = 100f;
                target.Data.Thirst = 100f;
            }
        }
        else
        {
            healthComp.Health = System.MathF.Min( healthComp.MaxHealth, healthComp.Health + amount );
        }
        return true;
    }
}
