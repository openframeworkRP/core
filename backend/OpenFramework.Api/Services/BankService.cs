using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.DTOs;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class BankService
{
    private readonly OpenFrameworkDbContext _context;

    public BankService(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────
    //  COMPTES
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Crée un compte bancaire et donne le rôle Owner au character demandeur.
    /// </summary>
    public async Task<BankAccount> CreateAccountAsync(string characterId, CreateAccountDto dto)
    {
        var accountNumber = await GenerateAccountNumberAsync();

            var account = new BankAccount
            {
                AccountNumber = accountNumber,
                AccountName = dto.AccountName,
                AccountType = dto.AccountType,
                BalanceCents = 0,
            };

            var ownerAccess = new AccountAccess
            {
                AccountId = account.Id,
                CharacterId = characterId,
                Role = BankRole.Owner,
                CanWithdraw = true,
                CanDeposit = true,
                CanTransfer = true,
                CanManageMembers = true,
            };

            _context.BankAccounts.Add(account);
            _context.AccountAccesses.Add(ownerAccess);
            await _context.SaveChangesAsync();

            return account;
    }

    public async Task<List<BankAccount>> GetAccountsForCharacterAsync(string characterId)
    {
        return await _context.AccountAccesses
            .Where(a => a.CharacterId == characterId)
            .Select(a => a.Account!)
            .Where(a => a.IsActive)
            .ToListAsync();
    }

    public async Task<BankAccount?> GetAccountByIdAsync(string accountId)
    {
        return await _context.BankAccounts
            .Include(a => a.Accesses)
            .FirstOrDefaultAsync(a => a.Id == accountId && a.IsActive);
    }

    // ─────────────────────────────────────────────────────────────
    //  MEMBRES
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Ajoute un member à un compte partagé.
    /// Seul un Owner ou Manager avec CanManageMembers peut le faire.
    /// </summary>
    public async Task<(bool Success, string Error)> AddMemberAsync(
        string requesterCharacterId, string accountId, AddMemberDto dto)
    {
        var requesterAccess = await GetAccessAsync(accountId, requesterCharacterId);
        if (requesterAccess == null)
            return (false, "Vous n'avez pas accès à ce compte.");

        if (!CanManageMembers(requesterAccess))
            return (false, "Vous n'avez pas la permission de gérer les membres.");

        var alreadyMember = await _context.AccountAccesses
            .AnyAsync(a => a.AccountId == accountId && a.CharacterId == dto.CharacterId);
        if (alreadyMember)
            return (false, "Ce personnage est déjà membre du compte.");

        // On ne peut pas ajouter quelqu'un avec un rôle supérieur au sien
        if (dto.Role == BankRole.Owner)
            return (false, "Impossible d'assigner le rôle Owner via cette route.");

        if (dto.Role == BankRole.Manager && requesterAccess.Role != BankRole.Owner)
            return (false, "Seul le Owner peut ajouter un Manager.");

        var access = new AccountAccess
        {
            AccountId = accountId,
            CharacterId = dto.CharacterId,
            Role = dto.Role,
            CanWithdraw = dto.CanWithdraw,
            CanDeposit = dto.CanDeposit,
            CanTransfer = dto.CanTransfer,
            CanManageMembers = dto.CanManageMembers,
        };

        _context.AccountAccesses.Add(access);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    /// <summary>Retire un membre. Un Owner ne peut pas être expulsé.</summary>
    public async Task<(bool Success, string Error)> RemoveMemberAsync(
        string requesterCharacterId, string accountId, string targetCharacterId)
    {
        var requesterAccess = await GetAccessAsync(accountId, requesterCharacterId);
        if (requesterAccess == null || !CanManageMembers(requesterAccess))
            return (false, "Permission insuffisante.");

        var target = await GetAccessAsync(accountId, targetCharacterId);
        if (target == null)
            return (false, "Ce personnage n'est pas membre du compte.");

        if (target.Role == BankRole.Owner)
            return (false, "Le propriétaire ne peut pas être retiré.");

        _context.AccountAccesses.Remove(target);
        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    public async Task<(bool Success, string Error)> UpdateMemberPermissionsAsync(
        string requesterCharacterId, string accountId,
        string targetCharacterId, UpdateMemberPermissionsDto dto)
    {
        var requesterAccess = await GetAccessAsync(accountId, requesterCharacterId);
        if (requesterAccess == null || !CanManageMembers(requesterAccess))
            return (false, "Permission insuffisante.");

        var target = await GetAccessAsync(accountId, targetCharacterId);
        if (target == null)
            return (false, "Membre introuvable.");

        if (target.Role == BankRole.Owner)
            return (false, "Impossible de modifier les permissions du Owner.");

        target.CanWithdraw = dto.CanWithdraw;
        target.CanDeposit = dto.CanDeposit;
        target.CanTransfer = dto.CanTransfer;
        target.CanManageMembers = dto.CanManageMembers;

        await _context.SaveChangesAsync();
        return (true, string.Empty);
    }

    // ─────────────────────────────────────────────────────────────
    //  TRANSACTIONS
    // ─────────────────────────────────────────────────────────────

    /// <summary>Virement entre deux comptes.</summary>
    public async Task<(bool Success, string Error, Transaction? Tx)> TransferAsync(
        string initiatorCharacterId, TransferDto dto)
    {
        if (dto.Amount <= 0)
            return (false, "Montant invalide.", null);

        var access = await GetAccessAsync(dto.FromAccountId, initiatorCharacterId);
        if (access == null)
            return (false, "Accès refusé au compte source.", null);

        if (!HasPermission(access, Permission.Transfer))
            return (false, "Vous n'avez pas la permission de faire des virements.", null);

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var from = await _context.BankAccounts.FindAsync(dto.FromAccountId);
            var to   = await _context.BankAccounts.FindAsync(dto.ToAccountId);

            if (from == null || !from.IsActive) return (false, "Compte source introuvable.", null);
            if (to == null || !to.IsActive)     return (false, "Compte destinataire introuvable.", null);

            var amountCents = ToCents(dto.Amount);
            if (from.BalanceCents < amountCents)
                return (false, "Solde insuffisant.", null);

            from.BalanceCents -= amountCents;
            to.BalanceCents   += amountCents;

            var transaction = new Transaction
            {
                FromAccountId = dto.FromAccountId,
                ToAccountId = dto.ToAccountId,
                InitiatorCharacterId = initiatorCharacterId,
                Type = TransactionType.Transfer,
                AmountCents = amountCents,
                Comment = dto.Comment,
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

    public async Task<List<Transaction>> GetTransactionsAsync(string accountId, int page = 1, int pageSize = 20)
    {
        return await _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS INTERNES
    // ─────────────────────────────────────────────────────────────

    public async Task<AccountAccess?> GetAccessAsync(string accountId, string characterId)
        => await _context.AccountAccesses
            .FirstOrDefaultAsync(a => a.AccountId == accountId && a.CharacterId == characterId);

    private static bool CanManageMembers(AccountAccess access)
        => access.Role is BankRole.Owner or BankRole.Manager
           || access.CanManageMembers;

    private enum Permission { Withdraw, Deposit, Transfer }

    private static bool HasPermission(AccountAccess access, Permission p)
    {
        // Owner et Manager ont tout
        if (access.Role is BankRole.Owner or BankRole.Manager) return true;
        return p switch
        {
            Permission.Withdraw => access.CanWithdraw,
            Permission.Deposit  => access.CanDeposit,
            Permission.Transfer => access.CanTransfer,
            _ => false
        };
    }

    private async Task<string> GenerateAccountNumberAsync()
    {
        // Format SL-XXXXX, incrémental
        var count = await _context.BankAccounts.CountAsync();
        return $"SL-{(count + 1):D5}";
    }

    public static long ToCents(decimal amount) => (long)Math.Round(amount * 100);
    public static decimal FromCents(long cents) => cents / 100m;
}