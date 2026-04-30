namespace OpenFramework.Api.Models.Administration.Logs;

/// <summary>
/// Un message de chat (in-game). Inclut les commandes (IsCommand=true) — le préfixe '/' est conservé.
/// Les joueurs sont prévenus de l'enregistrement à la création de leur personnage (mention CGU).
/// </summary>
public class ChatLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public string SteamId { get; set; } = "";

    /// <summary>Nom affiché (RP ou Steam) tel que vu par les autres joueurs.</summary>
    public string AuthorName { get; set; } = "";

    /// <summary>Canal du message : "global", "proximity", "ooc", "team", etc. Vide si inconnu.</summary>
    public string Channel { get; set; } = "";

    public string Message { get; set; } = "";

    public bool IsCommand { get; set; }
}
