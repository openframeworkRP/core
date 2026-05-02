using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/bank")]
[Authorize]
public class BankController : ControllerBase
{
    private readonly BankService _bank;
    private readonly CharacterService _characters;

    public BankController(BankService bank, CharacterService characters)
    {
        _bank = bank;
        _characters = characters;
    }

    private string GetSteamId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<(Character? Character, IActionResult? Error)> GetActiveCharacterAsync()
    {
        var steamId = GetSteamId();
        var character = await _characters.GetSelectedCharacterAsync(steamId);
        if (character == null)
            return (null, Forbid("Aucun personnage actif sélectionné."));
        return (character, null);
    }

    private static AccountView ToView(BankAccount a) => new(
        a.Id, a.AccountNumber, a.AccountName, a.AccountType,
        BankService.FromCents(a.BalanceCents), a.CreatedAt);

    private static TransactionView ToView(Transaction t) => new(
        t.Id, t.FromAccountId, t.ToAccountId, t.InitiatorCharacterId,
        t.Type, BankService.FromCents(t.AmountCents), t.Comment, t.Status, t.CreatedAt);

    // ── Comptes ───────────────────────────────────────────────────────────────

    [HttpPost("accounts/create")]
    public async Task<IActionResult> CreateAccount([FromBody] OpenAccountRequest request)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var account = await _bank.CreateAccountAsync(character!.Id, request);
        return Ok(ToView(account));
    }

    [HttpGet("accounts/{characterId}")]
    public async Task<IActionResult> GetMyAccounts(string characterId)
    {
        var character = await _characters.GetByIdAsync(characterId);
        var accounts = await _bank.GetAccountsForCharacterAsync(character!.Id);
        return Ok(accounts.Select(ToView));
    }

    [HttpGet("account/{accountId}")]
    public async Task<IActionResult> GetAccount(string accountId)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var access = await _bank.GetAccessAsync(accountId, character!.Id);
        if (access == null) return Forbid("Accès refusé.");

        var account = await _bank.GetAccountByIdAsync(accountId);
        if (account == null) return NotFound();

        return Ok(ToView(account));
    }

    // ── Membres ───────────────────────────────────────────────────────────────

    [HttpPost("accounts/{accountId}/members")]
    public async Task<IActionResult> AddMember(string accountId, [FromBody] AddAccountMemberRequest request)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err) = await _bank.AddMemberAsync(character!.Id, accountId, request);
        return success ? Ok() : BadRequest(new { message = err });
    }

    [HttpDelete("accounts/{accountId}/members/{targetCharacterId}")]
    public async Task<IActionResult> RemoveMember(string accountId, string targetCharacterId)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err) = await _bank.RemoveMemberAsync(character!.Id, accountId, targetCharacterId);
        return success ? Ok() : BadRequest(new { message = err });
    }

    [HttpPatch("accounts/{accountId}/members/{targetCharacterId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(
        string accountId, string targetCharacterId, [FromBody] MemberPermissionsRequest request)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err) = await _bank.UpdateMemberPermissionsAsync(character!.Id, accountId, targetCharacterId, request);
        return success ? Ok() : BadRequest(new { message = err });
    }

    // ── Transactions ──────────────────────────────────────────────────────────

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] MoneyTransferRequest request)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err, tx) = await _bank.TransferAsync(character!.Id, request);
        if (!success) return BadRequest(new { message = err });

        return Ok(ToView(tx!));
    }

    [HttpGet("accounts/{accountId}/transactions")]
    public async Task<IActionResult> GetTransactions(
        string accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var access = await _bank.GetAccessAsync(accountId, character!.Id);
        if (access == null) return Forbid("Accès refusé.");

        var txs = await _bank.GetTransactionsAsync(accountId, page, pageSize);
        return Ok(txs.Select(ToView));
    }
}
