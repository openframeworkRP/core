using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Administration.Logs;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize(Roles = "GameServer")]
public class EventsController : ControllerBase
{
    private readonly OpenFrameworkDbContext _db;

    public EventsController(OpenFrameworkDbContext db) { _db = db; }

    // ── Sessions ──────────────────────────────────────────────────────────────

    [HttpPost("session/join")]
    public async Task<IActionResult> SessionJoin([FromBody] PlayerJoinRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SteamId))
            return BadRequest(new { error = "SteamId requis" });

        var stale = await _db.Sessions.Where(s => s.SteamId == request.SteamId && s.LeftAt == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var s in stale)
        {
            s.LeftAt = now;
            s.DurationSeconds = (int)Math.Max(0, (now - s.JoinedAt).TotalSeconds);
        }

        var session = new Session
        {
            SteamId = request.SteamId,
            DisplayName = request.DisplayName ?? "",
            JoinedAt = now,
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { id = session.Id, joinedAt = session.JoinedAt });
    }

    [HttpPost("session/leave")]
    public async Task<IActionResult> SessionLeave([FromBody] PlayerLeaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SteamId))
            return BadRequest(new { error = "SteamId requis" });

        Session? session;
        if (request.SessionId.HasValue)
        {
            session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionId.Value);
        }
        else
        {
            session = await _db.Sessions
                .Where(s => s.SteamId == request.SteamId && s.LeftAt == null)
                .OrderByDescending(s => s.JoinedAt)
                .FirstOrDefaultAsync();
        }

        if (session == null) return NotFound(new { error = "Session active introuvable" });
        if (session.LeftAt != null) return Ok(new { id = session.Id, alreadyClosed = true });

        var now = DateTime.UtcNow;
        session.LeftAt = now;
        session.DurationSeconds = (int)Math.Max(0, (now - session.JoinedAt).TotalSeconds);
        await _db.SaveChangesAsync();

        return Ok(new { id = session.Id, leftAt = session.LeftAt, durationSeconds = session.DurationSeconds });
    }

    [HttpPost("session/close-all-stale")]
    public async Task<IActionResult> CloseAllStaleSessions()
    {
        var open = await _db.Sessions.Where(s => s.LeftAt == null).ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var s in open)
        {
            s.LeftAt = now;
            s.DurationSeconds = (int)Math.Max(0, (now - s.JoinedAt).TotalSeconds);
        }
        await _db.SaveChangesAsync();
        return Ok(new { closed = open.Count, at = now });
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SteamId) || request.Message == null)
            return BadRequest(new { error = "SteamId et Message requis" });

        var log = new ChatLog
        {
            SteamId    = request.SteamId,
            AuthorName = request.AuthorName ?? "",
            Channel    = request.Channel ?? "",
            Message    = request.Message,
            IsCommand  = request.IsCommand,
        };
        _db.ChatLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    // ── Admin action ──────────────────────────────────────────────────────────

    [HttpPost("admin-action")]
    public async Task<IActionResult> AdminAction([FromBody] AdminAuditRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AdminSteamId) || string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(new { error = "AdminSteamId et Action requis" });

        var log = new AdminActionLog
        {
            AdminSteamId  = request.AdminSteamId,
            Action        = request.Action,
            TargetSteamId = request.TargetSteamId,
            Reason        = request.Reason,
            PayloadJson   = request.PayloadJson,
            Source        = request.Source ?? "web",
        };
        _db.AdminActionLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    // ── Inventaire (audit anti-duplication) ───────────────────────────────────

    [HttpPost("inventory")]
    public async Task<IActionResult> Inventory([FromBody] InventoryEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ActorSteamId) || string.IsNullOrWhiteSpace(request.Action))
            return BadRequest(new { error = "ActorSteamId et Action requis" });

        var log = new InventoryLog
        {
            ActorSteamId = request.ActorSteamId,
            CharacterId  = request.CharacterId,
            Action       = request.Action,
            ItemGameId   = request.ItemGameId ?? "",
            Count        = request.Count,
            SourceType   = request.SourceType ?? "",
            SourceId     = request.SourceId,
            TargetType   = request.TargetType ?? "",
            TargetId     = request.TargetId,
            MetadataJson = request.MetadataJson,
        };
        _db.InventoryLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    [HttpPost("inventory/bulk")]
    public async Task<IActionResult> InventoryBulk([FromBody] InventoryEventBulkRequest request)
    {
        if (request.Logs == null || request.Logs.Count == 0)
            return BadRequest(new { error = "Logs[] requis" });

        var entries = request.Logs.Select(d => new InventoryLog
        {
            ActorSteamId = d.ActorSteamId ?? "",
            CharacterId  = d.CharacterId,
            Action       = d.Action ?? "",
            ItemGameId   = d.ItemGameId ?? "",
            Count        = d.Count,
            SourceType   = d.SourceType ?? "",
            SourceId     = d.SourceId,
            TargetType   = d.TargetType ?? "",
            TargetId     = d.TargetId,
            MetadataJson = d.MetadataJson,
        }).ToList();

        _db.InventoryLogs.AddRange(entries);
        await _db.SaveChangesAsync();
        return Ok(new { count = entries.Count });
    }
}
