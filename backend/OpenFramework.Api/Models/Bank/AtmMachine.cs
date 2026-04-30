namespace OpenFramework.Api.Models;

public class AtmMachine
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Identifiant de l'entité S&box (NetworkIdent ou nom de scène)</summary>
    public string GameEntityId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Position en jeu pour logs / debug</summary>
    public float PosX { get; set; }
    public float PosY { get; set; }
    public float PosZ { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation EF
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}