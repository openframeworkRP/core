using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop.Infrastructure;
using OpenFramework.Api.Data;
using OpenFramework.Api.DToS;
using OpenFramework.Api.Models;
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
    public async Task<IActionResult> BanPlayer([FromBody] AddUserBanDto dto)
    {
        if (string.IsNullOrEmpty(dto.UserSteamId))
        {
            return BadRequest("l'user est null ou invalide");
        }
        var user = _context.Bans.FirstOrDefault(t => t.SteamId == dto.UserSteamId);
        if (user != null)
        {
            return Conflict(new {success = false, error = "L'utilsateur est déjà ban"});
        }

        var banInfo = new UserBan()
        {
            Id = Guid.NewGuid().ToString(),
            SteamId = dto.UserSteamId,
            Reason = dto.Reason,
            FromAdminSteamId = dto.AdminSteamId
        };
        _context.Bans.Add(banInfo);
        await _context.SaveChangesAsync();
        return Ok(new
        {
            success = true,
        });
    }
    
    [HttpPost("unban/{userId}")]
    public async Task<IActionResult> UnBanUserAsync(string userId, [FromBody] RemoveUserBanDto dto)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest("l'user est null ou invalide");
        }
        var user = _context.Bans.FirstOrDefault(t => t.SteamId == userId);
        if (user == null)
        {
            return NotFound(new {success = false, error = "L'utilisateur n'est pas ban"});
        }
        _context.Bans.Remove(user);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("ban/getList")]
    public async Task<IActionResult> GetBanListAsync()
    {
        return Ok(await _context.Bans.ToListAsync());
    }

    [HttpPost("whitelist/")]
    public async Task<IActionResult> AddInWhiteListUser([FromBody] AddUserInWhitelistDto dto)
    {
        if (string.IsNullOrEmpty(dto.UserSteamId))
        {
            return BadRequest("l'user est null ou invalide");
        }
        var user = _context.Whitelists.FirstOrDefault(t => t.SteamId == dto.UserSteamId);
        if (user != null)
        {
            return Conflict(new {success = false, error = "L'utilisateur est déjà dans la whitelist."});
        }
        UserWhitelist userWhitelist = new UserWhitelist()
        {
            Id = Guid.NewGuid().ToString(),
            SteamId = dto.UserSteamId,
            FromAdminSteamId = dto.AdminSteamId
        };
        _context.Whitelists.Add(userWhitelist);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("whitelist/{userId}/supp")]
    public async Task<IActionResult> RemoveUserInWhitelist(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new  { success = false , error = "steam id invalid. "});
        }
        var user = _context.Whitelists.FirstOrDefault(t => t.SteamId == userId);
        if (user == null)
        {
            return NotFound(new {success = false, error = "L'utilisateur n'est pas dans la whitelist"});
        }
        
        _context.Whitelists.Remove(user);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("whitelist/getList")]
    public async Task<IActionResult> GetWhitelistListAsync()
    {
        return Ok(await _context.Whitelists.ToListAsync());
    }
}