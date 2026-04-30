namespace OpenFramework.Api.Models;

public class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Null si injection serveur (salaire, ATM cash creation)</summary>
    public string? FromAccountId { get; set; }

    /// <summary>Null si retrait pur vers le "vide" (destruction de monnaie)</summary>
    public string? ToAccountId { get; set; }

    /// <summary>Personnage qui a initié l'opération — null si opération serveur</summary>
    public string? InitiatorCharacterId { get; set; }

    /// <summary>ATM ayant servi à l'opération — null si transaction directe</summary>
    public string? AtmId { get; set; }

    public TransactionType Type { get; set; }

    /// <summary>Montant en centimes, toujours positif</summary>
    public long AmountCents { get; set; }

    public string? Comment { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Completed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation EF
    public BankAccount? FromAccount { get; set; }
    public BankAccount? ToAccount { get; set; }
    public AtmMachine? Atm { get; set; }
}

public enum TransactionType
{
    Deposit,       // Dépôt via ATM (cash -> compte)
    Withdrawal,    // Retrait via ATM (compte -> cash)
    Transfer,      // Virement entre comptes
    Salary,        // Injection automatique serveur
    AdminCreation  // Création arbitraire de monnaie (admin)
}

public enum TransactionStatus
{
    Completed,
    Failed,
    Reversed
}