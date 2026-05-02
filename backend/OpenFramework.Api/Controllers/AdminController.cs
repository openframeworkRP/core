using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Administration;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "GameServer")]
public class AdminController : Controller
{
    private readonly OpenFrameworkDbContext _context;

    public AdminController(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    [HttpPost("ban/")]
    public async Task<IActionResult> BanPlayer([FromBody] BanPlayerRequest request)
    {
        if (string.IsNullOrEmpty(request.UserSteamId))
            return BadRequest("l'user est null ou invalide");

        var existing = _context.Bans.FirstOrDefault(t => t.SteamId == request.UserSteamId);
        if (existing != null)
            return Conflict(new { success = false, error = "L'utilisateur est déjà banni." });

        _context.Bans.Add(new UserBan
        {
            Id = Guid.NewGuid().ToString(),
            SteamId = request.UserSteamId,
            Reason = request.Reason,
            FromAdminSteamId = request.AdminSteamId
        });
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpPost("unban/{userId}")]
    public async Task<IActionResult> UnBanUser(string userId, [FromBody] UnbanPlayerRequest request)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest("l'user est null ou invalide");

        var ban = _context.Bans.FirstOrDefault(t => t.SteamId == userId);
        if (ban == null)
            return NotFound(new { success = false, error = "L'utilisateur n'est pas banni." });

        _context.Bans.Remove(ban);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("ban/getList")]
    public async Task<IActionResult> GetBanList()
        => Ok(await _context.Bans.AsNoTracking().ToListAsync());

    [HttpPost("whitelist/")]
    public async Task<IActionResult> AddToWhitelist([FromBody] WhitelistPlayerRequest request)
    {
        if (string.IsNullOrEmpty(request.UserSteamId))
            return BadRequest("l'user est null ou invalide");

        var existing = _context.Whitelists.FirstOrDefault(t => t.SteamId == request.UserSteamId);
        if (existing != null)
            return Conflict(new { success = false, error = "L'utilisateur est déjà dans la whitelist." });

        _context.Whitelists.Add(new UserWhitelist
        {
            Id = Guid.NewGuid().ToString(),
            SteamId = request.UserSteamId,
            FromAdminSteamId = request.AdminSteamId
        });
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("whitelist/{userId}/supp")]
    public async Task<IActionResult> RemoveFromWhitelist(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest(new { success = false, error = "SteamId invalide." });

        var entry = _context.Whitelists.FirstOrDefault(t => t.SteamId == userId);
        if (entry == null)
            return NotFound(new { success = false, error = "L'utilisateur n'est pas dans la whitelist." });

        _context.Whitelists.Remove(entry);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("whitelist/getList")]
    public async Task<IActionResult> GetWhitelistList()
        => Ok(await _context.Whitelists.AsNoTracking().ToListAsync());
}
