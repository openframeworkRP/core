using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Administration;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/admin/game-admins")]
[Authorize(Roles = "GameServer")]
public class GameAdminController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;

    public GameAdminController(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    // Appelé par le gamemode (via ApiComponent) pour récupérer la liste.
    [HttpGet]
    public IActionResult GetList()
    {
        var ids = _context.GameAdmins.Select(a => a.SteamId).ToList();
        return Ok(new { steamIds = ids });
    }

    // Appelé par le website backend après chaque ajout/suppression.
    // Remplace la liste entière (source de vérité = website SQLite).
    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncRequest request)
    {
        if (request?.SteamIds == null)
            return BadRequest("steamIds requis");

        _context.GameAdmins.RemoveRange(_context.GameAdmins);
        foreach (var sid in request.SteamIds.Distinct())
        {
            if (!string.IsNullOrWhiteSpace(sid))
                _context.GameAdmins.Add(new GameAdminSteamId { SteamId = sid.Trim() });
        }
        await _context.SaveChangesAsync();
        return Ok(new { synced = request.SteamIds.Length });
    }

    public class SyncRequest
    {
        public string[] SteamIds { get; set; } = [];
    }
}
