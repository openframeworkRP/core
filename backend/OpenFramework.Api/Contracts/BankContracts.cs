using OpenFramework.Api.Models;

namespace OpenFramework.Api.Contracts;

// ── Comptes ───────────────────────────────────────────────────────────────────

public record OpenAccountRequest(
    string AccountName,
    AccountType AccountType
);

public record AddAccountMemberRequest(
    string CharacterId,
    BankRole Role,
    bool CanWithdraw,
    bool CanDeposit,
    bool CanTransfer,
    bool CanManageMembers
);

public record MemberPermissionsRequest(
    bool CanWithdraw,
    bool CanDeposit,
    bool CanTransfer,
    bool CanManageMembers
);

public record AccountView(
    string Id,
    string AccountNumber,
    string AccountName,
    AccountType AccountType,
    decimal Balance,
    DateTime CreatedAt
);

// ── Transactions ──────────────────────────────────────────────────────────────

public record MoneyTransferRequest(
    string FromAccountId,
    string ToAccountId,
    decimal Amount,
    string? Comment
);

public record TransactionView(
    string Id,
    string? FromAccountId,
    string? ToAccountId,
    string? InitiatorCharacterId,
    TransactionType Type,
    decimal Amount,
    string? Comment,
    TransactionStatus Status,
    DateTime CreatedAt
);

// ── ATM ───────────────────────────────────────────────────────────────────────

public record AtmDepositRequest(
    string AtmId,
    string ToAccountId,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record AtmWithdrawalRequest(
    string AtmId,
    string FromAccountId,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record AtmTransferRequest(
    string AtmId,
    string FromAccountId,
    string ToAccountNumber,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record SalaryPaymentRequest(
    string ToAccountId,
    decimal Amount,
    string Reason
);

public record AdminMoneyRequest(
    string ToAccountId,
    decimal Amount,
    string Reason
);

public record RegisterAtmRequest(
    string GameEntityId,
    string Label,
    float PosX,
    float PosY,
    float PosZ
);
