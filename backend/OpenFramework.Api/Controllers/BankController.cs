using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.DTOs;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/bank")]
[Authorize]  // JWT joueur standard
public class BankController : ControllerBase
{
    private readonly BankService _bank;
    private readonly CharacterService _characters;

    public BankController(BankService bank, CharacterService characters)
    {
        _bank = bank;
        _characters = characters;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    /// <summary>Récupère le steamId depuis le JWT du joueur.</summary>
    private string GetSteamId() => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Récupère le character actif du joueur.
    /// Retourne null + 403 si aucun character sélectionné.
    /// </summary>
    private async Task<(Character? Character, IActionResult? Error)> GetActiveCharacterAsync()
    {
        var steamId = GetSteamId();
        var character = await _characters.GetSelectedCharacterAsync(steamId);
        if (character == null)
            return (null, Forbid("Aucun personnage actif sélectionné."));
        return (character, null);
    }

    private static BankAccountDto ToDto(BankAccount a) => new(
        a.Id, a.AccountNumber, a.AccountName, a.AccountType,
        BankService.FromCents(a.BalanceCents), a.CreatedAt);

    private static TransactionDto ToDto(Transaction t) => new(
        t.Id, t.FromAccountId, t.ToAccountId, t.InitiatorCharacterId,
        t.Type, BankService.FromCents(t.AmountCents), t.Comment, t.Status, t.CreatedAt);

    // ─── Comptes ──────────────────────────────────────────────────

    [HttpPost("accounts/create")]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountDto dto)
    {
        Console.WriteLine("REQUËTE PROCEDEE");
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var account = await _bank.CreateAccountAsync(character!.Id, dto);
        return Ok(ToDto(account));
    }

    [HttpGet("accounts/{characterId}")]
    public async Task<IActionResult> GetMyAccounts(string characterId)
    {
        var character = await _characters.GetByIdAsync(characterId);

        var accounts = await _bank.GetAccountsForCharacterAsync(character!.Id);
        return Ok(accounts.Select(ToDto));
    }

    [HttpGet("accounts/{accountId}")]
    public async Task<IActionResult> GetAccount(string accountId)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        // Vérifie que le character a bien accès à ce compte
        var access = await _bank.GetAccessAsync(accountId, character!.Id);
        if (access == null) return Forbid("Accès refusé.");

        var account = await _bank.GetAccountByIdAsync(accountId);
        if (account == null) return NotFound();

        return Ok(ToDto(account));
    }

    // ─── Membres ──────────────────────────────────────────────────

    [HttpPost("accounts/{accountId}/members")]
    public async Task<IActionResult> AddMember(string accountId, [FromBody] AddMemberDto dto)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err) = await _bank.AddMemberAsync(character!.Id, accountId, dto);
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
        string accountId, string targetCharacterId,
        [FromBody] UpdateMemberPermissionsDto dto)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err) = await _bank.UpdateMemberPermissionsAsync(
            character!.Id, accountId, targetCharacterId, dto);
        return success ? Ok() : BadRequest(new { message = err });
    }

    // ─── Transactions ─────────────────────────────────────────────

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromBody] TransferDto dto)
    {
        var (character, error) = await GetActiveCharacterAsync();
        if (error != null) return error;

        var (success, err, tx) = await _bank.TransferAsync(character!.Id, dto);
        if (!success) return BadRequest(new { message = err });

        return Ok(ToDto(tx!));
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
        return Ok(txs.Select(ToDto));
    }
}