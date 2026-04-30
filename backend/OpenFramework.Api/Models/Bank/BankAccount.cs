namespace OpenFramework.Api.Models;

public class BankAccount
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Numéro lisible affiché en jeu ex: SL-00042</summary>
    public string AccountNumber { get; set; } = string.Empty;

    public string AccountName { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }

    /// <summary>Solde en centimes (int) pour éviter les erreurs de virgule flottante</summary>
    public long BalanceCents { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation EF
    public ICollection<AccountAccess> Accesses { get; set; } = new List<AccountAccess>();
    public ICollection<Transaction> OutgoingTransactions { get; set; } = new List<Transaction>();
    public ICollection<Transaction> IncomingTransactions { get; set; } = new List<Transaction>();
}

public enum AccountType
{
    Personal,
    Shared
}