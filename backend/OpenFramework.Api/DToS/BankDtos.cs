using OpenFramework.Api.Models;

namespace OpenFramework.Api.DTOs;

// ─── Bank Account ────────────────────────────────────────────────

public record CreateAccountDto(
    string AccountName,
    AccountType AccountType
);

public record AddMemberDto(
    string CharacterId,
    BankRole Role,
    bool CanWithdraw,
    bool CanDeposit,
    bool CanTransfer,
    bool CanManageMembers
);

public record UpdateMemberPermissionsDto(
    bool CanWithdraw,
    bool CanDeposit,
    bool CanTransfer,
    bool CanManageMembers
);

public record BankAccountDto(
    string Id,
    string AccountNumber,
    string AccountName,
    AccountType AccountType,
    decimal Balance,       // Converti depuis centimes pour le client
    DateTime CreatedAt
);

// ─── Transactions ────────────────────────────────────────────────

public record TransferDto(
    string FromAccountId,
    string ToAccountId,
    decimal Amount,
    string? Comment
);

public record TransactionDto(
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

// ─── ATM / Server ────────────────────────────────────────────────

public record AtmDepositDto(
    string AtmId,
    string ToAccountId,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record AtmWithdrawalDto(
    string AtmId,
    string FromAccountId,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record AtmTransferDto(
    string AtmId,
    string FromAccountId,
    string ToAccountNumber,
    string InitiatorCharId,
    decimal Amount,
    string? Comment
);

public record SalaryPaymentDto(
    string ToAccountId,
    decimal Amount,
    string Reason
);

public record AdminMoneyCreationDto(
    string ToAccountId,
    decimal Amount,
    string Reason
);

public record RegisterAtmDto(
    string GameEntityId,
    string Label,
    float PosX,
    float PosY,
    float PosZ
);

// ─── Server Auth ─────────────────────────────────────────────────

public record ServerLoginDto(string ServerSecret);