using System.Data;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class AtmService
{
    private readonly OpenFrameworkDbContext _context;

    public AtmService(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    // ── Gestion des ATM ───────────────────────────────────────────────────────

    public async Task<AtmMachine> RegisterAtmAsync(RegisterAtmRequest request)
    {
        var atm = new AtmMachine
        {
            GameEntityId = request.GameEntityId,
            Label = request.Label,
            PosX = request.PosX,
            PosY = request.PosY,
            PosZ = request.PosZ,
        };
        _context.AtmMachines.Add(atm);
        await _context.SaveChangesAsync();
        return atm;
    }

    public async Task<AtmMachine?> GetAtmByGameEntityIdAsync(string gameEntityId)
        => await _context.AtmMachines.FirstOrDefaultAsync(a => a.GameEntityId == gameEntityId && a.IsActive);

    public async Task<List<AtmMachine>> GetAllAtmsAsync()
        => await _context.AtmMachines.Where(a => a.IsActive).ToListAsync();

    // ── Dépôt via ATM (cash physique → compte) ────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmDepositAsync(
        AtmDepositRequest request, string initiatorCharacterId)
    {
        if (request.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(request.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == request.ToAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        // RepeatableRead : PostgreSQL garantit qu'aucune mise à jour concurrente ne
        // modifie la ligne entre le SELECT et l'UPDATE de la même transaction.
        using var dbTx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        try
        {
            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.ToAccountId && a.IsActive);
            if (account == null) return (false, "Compte introuvable.", null);

            var amountCents = BankService.ToCents(request.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,
                ToAccountId = request.ToAccountId,
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = request.AtmId,
                Type = TransactionType.Deposit,
                AmountCents = amountCents,
                Comment = request.Comment ?? $"Dépôt ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    // ── Retrait via ATM (compte → cash physique) ──────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmWithdrawalAsync(
        AtmWithdrawalRequest request, string initiatorCharacterId)
    {
        if (request.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(request.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == request.FromAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        using var dbTx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        try
        {
            var account = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.IsActive);
            if (account == null) return (false, "Compte introuvable.", null);

            var amountCents = BankService.ToCents(request.Amount);
            if (account.BalanceCents < amountCents)
                return (false, "Solde insuffisant.", null);

            account.BalanceCents -= amountCents;

            var transaction = new Transaction
            {
                FromAccountId = request.FromAccountId,
                ToAccountId = null,
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = request.AtmId,
                Type = TransactionType.Withdrawal,
                AmountCents = amountCents,
                Comment = request.Comment ?? $"Retrait ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    // ── Virement compte↔compte via ATM ────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AtmTransferAsync(
        AtmTransferRequest request, string initiatorCharacterId)
    {
        if (request.Amount <= 0) return (false, "Montant invalide.", null);

        var atm = await _context.AtmMachines.FindAsync(request.AtmId);
        if (atm == null || !atm.IsActive) return (false, "ATM introuvable ou inactif.", null);

        var access = await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == request.FromAccountId
                                   && a.CharacterId == initiatorCharacterId);
        if (access == null) return (false, "Accès refusé sur ce compte.", null);

        var toAccountInfo = await _context.BankAccounts
            .Where(a => a.AccountNumber == request.ToAccountNumber && a.IsActive)
            .Select(a => new { a.Id })
            .FirstOrDefaultAsync();
        if (toAccountInfo == null) return (false, "Compte destinataire introuvable.", null);

        if (toAccountInfo.Id == request.FromAccountId)
            return (false, "Impossible de virer sur le même compte.", null);

        // Serializable pour le double-lock entre deux comptes : SSI PostgreSQL
        // garantit qu'aucune transaction concurrente ne peut modifier les deux
        // comptes simultanément sans qu'une des deux soit rejouée.
        using var dbTx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var fromAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == request.FromAccountId && a.IsActive);
            var toAccount = await _context.BankAccounts
                .FirstOrDefaultAsync(a => a.Id == toAccountInfo.Id && a.IsActive);

            if (fromAccount == null) return (false, "Compte source introuvable.", null);
            if (toAccount == null)   return (false, "Compte destinataire introuvable.", null);

            var amountCents = BankService.ToCents(request.Amount);
            if (fromAccount.BalanceCents < amountCents)
                return (false, "Solde insuffisant.", null);

            fromAccount.BalanceCents -= amountCents;
            toAccount.BalanceCents   += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                InitiatorCharacterId = initiatorCharacterId,
                AtmId = request.AtmId,
                Type = TransactionType.Transfer,
                AmountCents = amountCents,
                Comment = request.Comment ?? $"Virement ATM [{atm.Label}]",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    // ── Salaire (injection automatique serveur) ────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> PaySalaryAsync(SalaryPaymentRequest request)
    {
        if (request.Amount <= 0) return (false, "Montant invalide.", null);

        var account = await _context.BankAccounts.FindAsync(request.ToAccountId);
        if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

        using var dbTx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        try
        {
            var amountCents = BankService.ToCents(request.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,
                ToAccountId = request.ToAccountId,
                InitiatorCharacterId = null,
                Type = TransactionType.Salary,
                AmountCents = amountCents,
                Comment = request.Reason,
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }

    // ── Création arbitraire (admin) ────────────────────────────────────────────

    public async Task<(bool Success, string Error, Transaction? Tx)> AdminCreateMoneyAsync(AdminMoneyRequest request)
    {
        if (request.Amount <= 0) return (false, "Montant invalide.", null);

        var account = await _context.BankAccounts.FindAsync(request.ToAccountId);
        if (account == null || !account.IsActive) return (false, "Compte introuvable.", null);

        using var dbTx = await _context.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        try
        {
            var amountCents = BankService.ToCents(request.Amount);
            account.BalanceCents += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = null,
                ToAccountId = request.ToAccountId,
                InitiatorCharacterId = null,
                Type = TransactionType.AdminCreation,
                AmountCents = amountCents,
                Comment = $"[ADMIN] {request.Reason}",
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await dbTx.CommitAsync();
            return (true, string.Empty, transaction);
        }
        catch
        {
            await dbTx.RollbackAsync();
            throw;
        }
    }
}
