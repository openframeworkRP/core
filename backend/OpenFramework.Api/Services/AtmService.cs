using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.DTOs;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class AtmService
{
    private readonly OpenFrameworkDbContext _context;

    public AtmService(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────
    //  GESTION DES ATM
    // ─────────────────────────────────────────────────────────────

    public async Task<AtmMachine> RegisterAtmAsync(RegisterAtmDto dto)
    {
        var atm = new AtmMachine
        {
            GameEntityId = dto.GameEntityId,
            Label = dto.Label,
            PosX = dto.PosX,
            PosY = dto.PosY,
            PosZ = dto.PosZ,
        };
        _context.AtmMachines.Add(atm);
        await _context.SaveChangesAsync();
        return atm;
    }

    public async Task<AtmMachine?> GetAtmByGameEntityIdAsync(string gameEntityId)
        => await _context.AtmMachines.FirstOrDefaultAsync(a => a.GameEntityId == gameEntityId && a.IsActive);

    public async Task<List<AtmMachine>> GetAllAtmsAsync()
        => await _context.AtmMachines.Where(a => a.IsActive).ToListAsync();

    // ─────────────────────────────────────────────────────────────
    //  DÉPÔT via ATM (cash physique -> compte)
    //  Seul le serveur peut appeler ça — le joueur remet du cash
    //  et le serveur crédite son compte.
    // ─────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmDepositAsync(
        AtmDepositDto dto, string initiatorCharacterId)
    {
        if (dto.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(dto.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == dto.ToAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var account = await _context.BankAccounts
                .FromSqlInterpolated($"SELECT * FROM BankAccounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {dto.ToAccountId}")
                .FirstOrDefaultAsync();
            if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

            var amountCents = BankService.ToCents(dto.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,           // Vient du "cash physique" — pas de compte source
                ToAccountId = dto.ToAccountId,
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = dto.AtmId,
                Type = TransactionType.Deposit,
                AmountCents = amountCents,
                Comment = dto.Comment ?? $"Dépôt ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  RETRAIT via ATM (compte -> cash physique)
    // ─────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmWithdrawalAsync(
        AtmWithdrawalDto dto, string initiatorCharacterId)
    {
        if (dto.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(dto.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == dto.FromAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var account = await _context.BankAccounts
                .FromSqlInterpolated($"SELECT * FROM BankAccounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {dto.FromAccountId}")
                .FirstOrDefaultAsync();
            if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

            var amountCents = BankService.ToCents(dto.Amount);
            if (account.BalanceCents < amountCents)
                return (false, "Solde insuffisant.", null);

            account.BalanceCents -= amountCents;

            var transaction = new Transaction
            {
                FromAccountId = dto.FromAccountId,
                ToAccountId = null,             // Part dans le "cash physique"
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = dto.AtmId,
                Type = TransactionType.Withdrawal,
                AmountCents = amountCents,
                Comment = dto.Comment ?? $"Retrait ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  VIREMENT compte↔compte via ATM
    // ─────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmTransferAsync(
        AtmTransferDto dto, string initiatorCharacterId)
    {
        if (dto.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(dto.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == dto.FromAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        var toAccountInfo = await _context.BankAccounts
            .Where(a => a.AccountNumber == dto.ToAccountNumber && a.IsActive)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (toAccountInfo == null) return (false, "Compte destinataire introuvable.", null);

        if (toAccountInfo.Id == dto.FromAccountId)
            return (false, "Impossible de virer sur le même compte.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Lock des deux comptes — ordre stable par Id pour éviter les deadlocks
            var ids = new[] { dto.FromAccountId, toAccountInfo.Id }
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();

            var firstLocked = await _context.BankAccounts
                .FromSqlInterpolated($"SELECT * FROM BankAccounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {ids[0]}")
                .FirstOrDefaultAsync();
            var secondLocked = await _context.BankAccounts
                .FromSqlInterpolated($"SELECT * FROM BankAccounts WITH (UPDLOCK, ROWLOCK) WHERE Id = {ids[1]}")
                .FirstOrDefaultAsync();

            var fromAccount = firstLocked?.Id == dto.FromAccountId ? firstLocked : secondLocked;
            var toAccount = firstLocked?.Id == toAccountInfo.Id ? firstLocked : secondLocked;

            if (fromAccount == null || !fromAccount.IsActive)
                return (false, "Compte source introuvable.", null);
            if (toAccount == null || !toAccount.IsActive)
                return (false, "Compte destinataire introuvable.", null);

            var amountCents = BankService.ToCents(dto.Amount);
            if (fromAccount.BalanceCents < amountCents)
                return (false, "Solde insuffisant.", null);

            fromAccount.BalanceCents -= amountCents;
            toAccount.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = dto.AtmId,
                Type = TransactionType.Transfer,
                AmountCents = amountCents,
                Comment = dto.Comment ?? $"Virement ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  SALAIRE (injection automatique serveur)
    // ─────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> PaySalaryAsync(SalaryPaymentDto dto)
    {
        if (dto.Amount <= 0) return (false, "Montant invalide.", null);

        var account = await _context.BankAccounts.FindAsync(dto.ToAccountId);
        if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var amountCents = BankService.ToCents(dto.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,
                ToAccountId = dto.ToAccountId,
                InitiatorCharacterId = null,    // Initié par le serveur
                Type = TransactionType.Salary,
                AmountCents = amountCents,
                Comment = dto.Reason,
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  CRÉATION ARBITRAIRE (admin)
    // ─────────────────────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AdminCreateMoneyAsync(AdminMoneyCreationDto dto)
    {
        if (dto.Amount <= 0) return (false, "Montant invalide.", null);

        var account = await _context.BankAccounts.FindAsync(dto.ToAccountId);
        if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var amountCents = BankService.ToCents(dto.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,
                ToAccountId = dto.ToAccountId,
                InitiatorCharacterId = null,
                Type = TransactionType.AdminCreation,
                AmountCents = amountCents,
                Comment = $"[ADMIN] {dto.Reason}",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}