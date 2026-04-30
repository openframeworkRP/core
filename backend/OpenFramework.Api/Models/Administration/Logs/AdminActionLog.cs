namespace OpenFramework.Api.Models.Administration.Logs;

/// <summary>
/// Trace une action administrative (ban, unban, whitelist add/remove, kick, warn, …)
/// quelle que soit son origine (panel web ou commande in-game). Centralise tout l'historique
/// de modération pour audit et timeline unique côté front.
/// </summary>
public class AdminActionLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime At { get; set; } = DateTime.UtcNow;

    /// <summary>SteamId de l'admin qui a effectué l'action. "system" si automatique.</summary>
    public string AdminSteamId { get; set; } = "";

    /// <summary>
    /// Type d'action : "ban", "unban", "whitelist_add", "whitelist_remove", "kick", "warn",
    /// "character_delete", … Conservé en string pour rester ouvert à de nouvelles actions
    /// sans migration.
    /// </summary>
    public string Action { get; set; } = "";

    /// <summary>SteamId de la cible si applicable.</summary>
    public string? TargetSteamId { get; set; }

    public string? Reason { get; set; }

    /// <summary>JSON libre pour metadata supplémentaires (durée de ban, montant warn, etc.).</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Origine : "web" (panel web) ou "ingame" (commande staff in-game).</summary>
    public string Source { get; set; } = "web";
}
