using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

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
        _atm = atm;
    }

    [HttpGet("account/{characterId}")]
    public async Task<IActionResult> GetAccount(string characterId)
    {
        var access = await _context.AccountAccesses
            .AsNoTracking()
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
            CharacterId = characterId,
            Balance     = BankService.FromCents(access.Account.BalanceCents),
            access.Account.IsActive,
            access.Account.CreatedAt
        });
    }

    [HttpGet("transactions/{accountId}")]
    public async Task<IActionResult> GetTransactions(string accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Type,
                Amount = BankService.FromCents(t.AmountCents),
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

    [HttpPost("deposit")]
    public async Task<IActionResult> Deposit([FromBody] AtmDepositRequest request)
    {
        var (success, error, tx) = await _atm.AtmDepositAsync(request, request.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    [HttpPost("withdrawal")]
    public async Task<IActionResult> Withdrawal([FromBody] AtmWithdrawalRequest request)
    {
        var (success, error, tx) = await _atm.AtmWithdrawalAsync(request, request.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] AtmTransferRequest request)
    {
        var (success, error, tx) = await _atm.AtmTransferAsync(request, request.InitiatorCharId);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }

    [HttpPost("salary")]
    public async Task<IActionResult> PaySalary([FromBody] SalaryPaymentRequest request)
    {
        var (success, error, tx) = await _atm.PaySalaryAsync(request);
        if (!success) return BadRequest(new { success = false, error });
        return Ok(new { success = true, txId = tx!.Id });
    }
}
