namespace OpenFramework.Api.Models;

public class AccountAccess
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string AccountId { get; set; } = string.Empty;
    public string CharacterId { get; set; } = string.Empty;
    public BankRole Role { get; set; } = BankRole.Member;

    // Permissions granulaires (ignorées si Role == Owner)
    public bool CanWithdraw { get; set; }
    public bool CanDeposit { get; set; }
    public bool CanTransfer { get; set; }
    public bool CanManageMembers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation EF
    public BankAccount? Account { get; set; }
}

public enum BankRole
{
    Owner,   // Créateur — toutes permissions, inexpulsable
    Manager, // Toutes opérations + gestion membres
    Member   // Permissions définies manuellement
}