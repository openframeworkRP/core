namespace OpenFramework.Api.Models.Administration;

/// <summary>
/// Commande admin déposée par le panel web et en attente d'exécution par le
/// gamemode (qui poll toutes les 5s). Une fois exécutée, le gamemode pousse
/// le résultat via /command/{id}/result.
///
/// État : pending → processing (au moment où le gamemode la prend) → processed
/// ou failed. Le champ Result contient un message libre (succès, erreur).
/// </summary>
public class PendingAdminCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SteamId de l'admin web qui a déposé la commande.</summary>
    public string RequestedByAdminSteamId { get; set; } = "";

    /// <summary>Nom de la commande : kick, ban, givemoney, heal, giveitem, etc.</summary>
    public string Command { get; set; } = "";

    public string? TargetSteamId { get; set; }

    /// <summary>Args sérialisés en JSON ({"amount":1000} ou {"reason":"...","duration":60}).</summary>
    public string? ArgsJson { get; set; }

    public string Status { get; set; } = "pending"; // pending / processing / processed / failed

    public DateTime? ProcessedAt { get; set; }

    public string? Result { get; set; }
}
