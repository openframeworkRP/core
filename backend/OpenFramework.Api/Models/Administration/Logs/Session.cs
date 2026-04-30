namespace OpenFramework.Api.Models.Administration.Logs;

/// <summary>
/// Une session de jeu = un cycle join → leave. LeftAt null = session toujours active.
/// DurationSeconds est calculée et stockée au moment du leave pour requêtes rapides.
/// </summary>
public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>SteamId du joueur (string pour rester cohérent avec Users.Id).</summary>
    public string SteamId { get; set; } = "";

    /// <summary>DisplayName Steam au moment du join (pour historique lisible).</summary>
    public string DisplayName { get; set; } = "";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null tant que la session est active.</summary>
    public DateTime? LeftAt { get; set; }

    /// <summary>Renseigné au leave. Permet d'agréger les temps de jeu sans recalcul.</summary>
    public int? DurationSeconds { get; set; }
}
