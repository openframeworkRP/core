using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Administration;

namespace OpenFramework.Api.Controllers;

/// <summary>
/// Queue de commandes admin déposées par le panel web et exécutées par
/// le gamemode (poll toutes les 5s).
/// Toutes les routes exigent le JWT GameServer (le panel web passe par
/// le backend Node qui possède ce JWT).
/// </summary>
[ApiController]
[Route("api/admin/command")]
[Authorize(Roles = "GameServer")]
public class AdminCommandController : ControllerBase
{
    private readonly OpenFrameworkDbContext _db;

    public AdminCommandController(OpenFrameworkDbContext db) { _db = db; }

    // ── POST /queue : web admin dépose une commande ─────────────────────────
    [HttpPost("queue")]
    public async Task<IActionResult> Queue([FromBody] QueueCommandDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Command) || string.IsNullOrWhiteSpace(dto.AdminSteamId))
            return BadRequest(new { error = "Command et AdminSteamId requis" });

        var cmd = new PendingAdminCommand
        {
            RequestedByAdminSteamId = dto.AdminSteamId,
            Command       = dto.Command.Trim().ToLowerInvariant(),
            TargetSteamId = dto.TargetSteamId,
            ArgsJson      = dto.ArgsJson,
        };
        _db.PendingAdminCommands.Add(cmd);
        await _db.SaveChangesAsync();

        return Ok(new { id = cmd.Id, status = cmd.Status });
    }

    // ── GET /pending : gamemode récupère et marque comme processing ─────────
    // Retourne les commandes pending et les passe à processing en une seule
    // transaction pour éviter qu'un poll concurrent les exécute deux fois.
    [HttpGet("pending")]
    public async Task<IActionResult> Pending([FromQuery] int max = 20)
    {
        max = Math.Clamp(max, 1, 100);

        // Reset des commandes processing trop anciennes (> 30s) — un crash du
        // gamemode laisserait sinon les commandes bloquées pour toujours.
        var stuckBefore = DateTime.UtcNow.AddSeconds(-30);
        await _db.PendingAdminCommands
            .Where(c => c.Status == "processing" && c.CreatedAt < stuckBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Status, "pending"));

        var pending = await _db.PendingAdminCommands
            .Where(c => c.Status == "pending")
            .OrderBy(c => c.CreatedAt)
            .Take(max)
            .ToListAsync();

        foreach (var c in pending) c.Status = "processing";
        await _db.SaveChangesAsync();

        return Ok(pending);
    }

    // ── POST /{id}/result : gamemode pousse le résultat d'exécution ─────────
    [HttpPost("{id:guid}/result")]
    public async Task<IActionResult> Result(Guid id, [FromBody] CommandResultDto dto)
    {
        var cmd = await _db.PendingAdminCommands.FirstOrDefaultAsync(c => c.Id == id);
        if (cmd == null) return NotFound();

        cmd.Status      = dto.Success ? "processed" : "failed";
        cmd.ProcessedAt = DateTime.UtcNow;
        cmd.Result      = dto.Result;
        await _db.SaveChangesAsync();

        return Ok(new { id = cmd.Id, status = cmd.Status });
    }

    // ── GET / : liste pour le panel web (debug + suivi) ─────────────────────
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] int limit = 50)
    {
        limit = Math.Clamp(limit, 1, 200);
        var q = _db.PendingAdminCommands.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(c => c.Status == status);
        var rows = await q.OrderByDescending(c => c.CreatedAt).Take(limit).ToListAsync();
        return Ok(rows);
    }

    // ── GET /{id} : statut d'une commande précise — utilisé par le panel
    // pour polling sans tirer toute la liste ───────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var cmd = await _db.PendingAdminCommands.FirstOrDefaultAsync(c => c.Id == id);
        if (cmd == null) return NotFound();
        return Ok(cmd);
    }
}

public class QueueCommandDto
{
    public string AdminSteamId { get; set; } = "";
    public string Command { get; set; } = "";
    public string? TargetSteamId { get; set; }
    public string? ArgsJson { get; set; }
}

public class CommandResultDto
{
    public bool Success { get; set; }
    public string? Result { get; set; }
}
