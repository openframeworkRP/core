using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Data;
using OpenFramework.Api.DTOs;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace OpenFramework.Api.Controllers;

/// <summary>
/// Endpoints ATM — accessibles UNIQUEMENT par le serveur de jeu (JWT GameServer).
/// Le joueur ne peut jamais appeler ces routes directement.
/// </summary>
[ApiController]
[Route("api/atm")]
[Authorize(Roles = "GameServer")]
public class AtmController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;
    private readonly AtmService _atm;

    public AtmController(OpenFrameworkDbContext context, AtmService atm)
    {
        _context = context;
        _atm     = atm;
    }

    // ─────────────────────────────────────────────────────────────
    //  COMPTE — récupère le compte principal d'un character
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne le compte bancaire personnel du character.
    /// Appelé par le serveur S&box pour afficher le solde dans l'UI ATM.
    /// </summary>
    [HttpGet("account/{characterId}")]
    public async Task<IActionResult> GetAccount(string characterId)
    {
        var access = await _context.AccountAccesses
            .Include(a => a.Account)
            .Where(a => a.CharacterId == characterId
                     && a.Account!.IsActive
                     && a.Account.AccountType == AccountType.Personal)
            .FirstOrDefaultAsync();

        if (access?.Account == null)
            return NotFound($"Aucun compte personnel pour le character {characterId}.");
        
        return Ok(new
        {
            access.Account.Id,
            access.Account.AccountNumber,
            access.Account.AccountName,
            CharacterId  = characterId,
            Balance      = BankService.FromCents(access.Account.BalanceCents),
            access.Account.IsActive,
            access.Account.CreatedAt
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  TRANSACTIONS — historique d'un compte
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Retourne les 20 dernières transactions d'un compte.
    /// Appelé par le serveur S&box pour l'historique dans l'UI ATM.
    /// </summary>
    [HttpGet("transactions/{accountId}")]
    public async Task<IActionResult> GetTransactions(string accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var transactions = await _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Type,
                Amount   = BankService.FromCents(t.AmountCents),
                t.FromAccountId,
                t.ToAccountId,
                FromAccountNumber = t.FromAccount != null ? t.FromAccount.AccountNumber : null,
                ToAccountNumber   = t.ToAccount   != null ? t.ToAccount.AccountNumber   : null,
                t.Comment,
                t.Status,
                t.CreatedAt
            })
            .ToListAsync();

        return Ok(transactions);
    }

    // ─────────────────────────────────────────────────────────────
    //  DÉPÔT — cash physique → compte
    // ─────────────────────────────────────────────────────────────

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] AtmDepositDto dto)
    {
        var (success, error, tx) = await _atm.AtmDepositAsync(dto, dto.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    // ─────────────────────────────────────────────────────────────
    //  RETRAIT — compte → cash physique
    // ─────────────────────────────────────────────────────────────

    [HttpPost("withdrawal")]
    public async Task<IActionResult> Withdrawal([FromBody] AtmWithdrawalDto dto)
    {
        var (success, error, tx) = await _atm.AtmWithdrawalAsync(dto, dto.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    // ─────────────────────────────────────────────────────────────
    //  VIREMENT — compte → compte
    // ─────────────────────────────────────────────────────────────

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] AtmTransferDto dto)
    {
        var (success, error, tx) = await _atm.AtmTransferAsync(dto, dto.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    // ─────────────────────────────────────────────────────────────
    //  SALAIRE — credit serveur sur un compte (initie par GameServer uniquement)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Verse un salaire sur un compte. Seul le serveur de jeu (Roles = GameServer)
    /// peut appeler cette route — un client ne peut pas crediter son propre compte.
    /// </summary>
    [HttpPost("salary")]
    public async Task<IActionResult> PaySalary([FromBody] SalaryPaymentDto dto)
    {
        var (success, error, tx) = await _atm.PaySalaryAsync(dto);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }
}