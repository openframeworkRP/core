using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Administration.Logs;

namespace OpenFramework.Api.Controllers;

/// <summary>
/// Endpoints d'écriture pour le système d'audit (sessions joueur, chat, actions admin).
/// Tous les endpoints exigent le JWT GameServer — appelés par le gamemode s&box ou
/// par le backend Node (panel web).
/// </summary>
[ApiController]
[Route("api/events")]
[Authorize(Roles = "GameServer")]
public class EventsController : ControllerBase
{
    private readonly OpenFrameworkDbContext _db;

    public EventsController(OpenFrameworkDbContext db) { _db = db; }

    // ─────────────────────────────────────────────────────────────
    //  SESSIONS — join / leave
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Crée une nouvelle session (join). Renvoie l'Id pour permettre au caller de
    /// le réutiliser au leave (utile en cas de race condition entre plusieurs sessions
    /// successives très rapprochées d'un même joueur).
    /// </summary>
    [HttpPost("session/join")]
    public async Task<IActionResult> SessionJoin([FromBody] SessionJoinDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SteamId))
            return BadRequest(new { error = "SteamId requis" });

        // Sécurité : si une session précédente est restée ouverte (crash gamemode, kill -9),
        // on la ferme avant d'en ouvrir une nouvelle pour éviter l'accumulation de sessions
        // fantômes "actives".
        var stale = await _db.Sessions
            .Where(s => s.SteamId == dto.SteamId && s.LeftAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var s in stale)
        {
            s.LeftAt = now;
            s.DurationSeconds = (int)Math.Max(0, (now - s.JoinedAt).TotalSeconds);
        }

        var session = new Session
        {
            SteamId     = dto.SteamId,
            DisplayName = dto.DisplayName ?? "",
            JoinedAt    = now,
        };
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync();

        return Ok(new { id = session.Id, joinedAt = session.JoinedAt });
    }

    /// <summary>
    /// Ferme la session ouverte du joueur. Si SessionId est fourni, ferme cette session précise ;
    /// sinon ferme la dernière session active du SteamId.
    /// </summary>
    [HttpPost("session/leave")]
    public async Task<IActionResult> SessionLeave([FromBody] SessionLeaveDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SteamId))
            return BadRequest(new { error = "SteamId requis" });

        Session? session;
        if (dto.SessionId.HasValue)
        {
            session = await _db.Sessions.FirstOrDefaultAsync(s => s.Id == dto.SessionId.Value);
        }
        else
        {
            session = await _db.Sessions
                .Where(s => s.SteamId == dto.SteamId && s.LeftAt == null)
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

    /// <summary>
    /// Ferme toutes les sessions encore "ouvertes" (LeftAt = null). Appelé par le
    /// gamemode au boot pour purger les sessions fantômes laissées par un crash.
    /// L'API et le gamemode étant dissociés, l'API ne peut pas le faire elle-même.
    /// </summary>
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

    // ─────────────────────────────────────────────────────────────
    //  CHAT
    // ─────────────────────────────────────────────────────────────

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.SteamId) || dto.Message == null)
            return BadRequest(new { error = "SteamId et Message requis" });

        var log = new ChatLog
        {
            SteamId    = dto.SteamId,
            AuthorName = dto.AuthorName ?? "",
            Channel    = dto.Channel ?? "",
            Message    = dto.Message,
            IsCommand  = dto.IsCommand,
        };
        _db.ChatLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    // ─────────────────────────────────────────────────────────────
    //  ADMIN ACTION
    // ─────────────────────────────────────────────────────────────

    [HttpPost("admin-action")]
    public async Task<IActionResult> AdminAction([FromBody] AdminActionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.AdminSteamId) || string.IsNullOrWhiteSpace(dto.Action))
            return BadRequest(new { error = "AdminSteamId et Action requis" });

        var log = new AdminActionLog
        {
            AdminSteamId  = dto.AdminSteamId,
            Action        = dto.Action,
            TargetSteamId = dto.TargetSteamId,
            Reason        = dto.Reason,
            PayloadJson   = dto.PayloadJson,
            Source        = dto.Source ?? "web",
        };
        _db.AdminActionLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    // ─────────────────────────────────────────────────────────────
    //  INVENTORY (transferts d'items pour audit anti-duplication)
    // ─────────────────────────────────────────────────────────────

    [HttpPost("inventory")]
    public async Task<IActionResult> Inventory([FromBody] InventoryLogDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ActorSteamId) || string.IsNullOrWhiteSpace(dto.Action))
            return BadRequest(new { error = "ActorSteamId et Action requis" });

        var log = new InventoryLog
        {
            ActorSteamId = dto.ActorSteamId,
            CharacterId  = dto.CharacterId,
            Action       = dto.Action,
            ItemGameId   = dto.ItemGameId ?? "",
            Count        = dto.Count,
            SourceType   = dto.SourceType ?? "",
            SourceId     = dto.SourceId,
            TargetType   = dto.TargetType ?? "",
            TargetId     = dto.TargetId,
            MetadataJson = dto.MetadataJson,
        };
        _db.InventoryLogs.Add(log);
        await _db.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    /// <summary>
    /// Endpoint bulk pour les opérations qui génèrent beaucoup d'events d'un coup
    /// (ex: SaveSnapshot avec 30 items = 30 logs en une transaction au lieu de 30 round-trips).
    /// </summary>
    [HttpPost("inventory/bulk")]
    public async Task<IActionResult> InventoryBulk([FromBody] InventoryLogBulkDto dto)
    {
        if (dto.Logs == null || dto.Logs.Count == 0)
            return BadRequest(new { error = "Logs[] requis" });

        var entries = dto.Logs.Select(d => new InventoryLog
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

// ── DTOs ─────────────────────────────────────────────────────────────────────

public class SessionJoinDto
{
    public string SteamId { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class SessionLeaveDto
{
    public string SteamId { get; set; } = "";
    public Guid? SessionId { get; set; }
}

public class ChatLogDto
{
    public string SteamId { get; set; } = "";
    public string? AuthorName { get; set; }
    public string? Channel { get; set; }
    public string Message { get; set; } = "";
    public bool IsCommand { get; set; }
}

public class AdminActionDto
{
    public string AdminSteamId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? TargetSteamId { get; set; }
    public string? Reason { get; set; }
    public string? PayloadJson { get; set; }
    public string? Source { get; set; }
}

public class InventoryLogDto
{
    public string ActorSteamId { get; set; } = "";
    public string? CharacterId { get; set; }
    public string Action { get; set; } = "";
    public string? ItemGameId { get; set; }
    public int Count { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? MetadataJson { get; set; }
}

public class InventoryLogBulkDto
{
    public List<InventoryLogDto> Logs { get; set; } = new();
}
