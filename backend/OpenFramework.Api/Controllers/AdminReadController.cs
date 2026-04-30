using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

/// <summary>
/// Routes de lecture seule pour le centre d'administration web.
/// Toutes les routes exigent le JWT GameServer (secret partagé).
/// Aucune modification d'entités ni de schéma — uniquement des SELECT sur les tables existantes.
/// </summary>
[ApiController]
[Route("api/admin/read")]
[Authorize(Roles = "GameServer")]
public class AdminReadController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;

    public AdminReadController(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    // ─────────────────────────────────────────────────────────────
    //  USERS — liste + détail
    // ─────────────────────────────────────────────────────────────

    /// <summary>Liste tous les utilisateurs connus (table Users) avec statut de modération et compte de characters.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.Users.ToListAsync();
        var steamIds = users.Select(u => u.Id).ToList();

        var charactersByOwner = await _context.Characters
            .Where(c => steamIds.Contains(c.OwnerId))
            .GroupBy(c => c.OwnerId)
            .Select(g => new { OwnerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OwnerId, x => x.Count);

        var bans       = await _context.Bans.Select(b => b.SteamId).ToListAsync();
        var whitelists = await _context.Whitelists.Select(w => w.SteamId).ToListAsync();
        var warnsBySteamId = (await _context.Warns.ToListAsync())
            .GroupBy(w => w.SteamId)
            .ToDictionary(g => g.Key, g => g.Count());

        var result = users.Select(u => new
        {
            steamId      = u.Id,
            characters   = charactersByOwner.GetValueOrDefault(u.Id, 0),
            isBanned     = bans.Contains(u.Id),
            isWhitelisted= whitelists.Contains(u.Id),
            warnCount    = warnsBySteamId.GetValueOrDefault(u.Id, 0),
        });

        return Ok(result);
    }

    /// <summary>Détail d'un utilisateur : characters, ban, whitelist, warns.</summary>
    [HttpGet("users/{steamId}")]
    public async Task<IActionResult> GetUser(string steamId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == steamId);
        if (user == null) return NotFound(new { error = "Utilisateur introuvable." });

        var characters = await _context.Characters
            .Where(c => c.OwnerId == steamId)
            .ToListAsync();

        var ban        = await _context.Bans.FirstOrDefaultAsync(b => b.SteamId == steamId);
        var whitelist  = await _context.Whitelists.FirstOrDefaultAsync(w => w.SteamId == steamId);
        var warns      = await _context.Warns.Where(w => w.SteamId == steamId).ToListAsync();

        return Ok(new
        {
            steamId      = user.Id,
            characters,
            ban,
            whitelist,
            warns,
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  CHARACTERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>Liste tous les characters (tous joueurs confondus).</summary>
    [HttpGet("characters")]
    public async Task<IActionResult> GetCharacters()
    {
        var characters = await _context.Characters.ToListAsync();
        return Ok(characters);
    }

    /// <summary>Détail complet d'un character : infos + position + inventaire + comptes bancaires.</summary>
    [HttpGet("characters/{id}")]
    public async Task<IActionResult> GetCharacter(string id)
    {
        var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return NotFound(new { error = "Character introuvable." });

        var position  = await _context.CharacterPositions.FirstOrDefaultAsync(p => p.CharacterId == id);
        var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.OwnerId == id);

        var items = inventory != null
            ? await _context.Items.Where(it => it.InventoryId == inventory.Id).ToListAsync()
            : new List<InventoryItem>();

        var accountIds = await _context.AccountAccesses
            .Where(a => a.CharacterId == id)
            .Select(a => a.AccountId)
            .ToListAsync();

        var accounts = await _context.BankAccounts
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.AccountNumber,
                a.AccountName,
                a.AccountType,
                Balance = BankService.FromCents(a.BalanceCents),
                a.IsActive,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(new
        {
            character,
            position,
            inventory,
            items,
            accounts,
        });
    }

    /// <summary>Inventaire d'un character (items seuls).</summary>
    [HttpGet("characters/{id}/inventory")]
    public async Task<IActionResult> GetCharacterInventory(string id)
    {
        var inventory = await _context.Inventories.FirstOrDefaultAsync(inv => inv.OwnerId == id);
        if (inventory == null) return Ok(new { inventory = (object?)null, items = Array.Empty<InventoryItem>() });

        var items = await _context.Items.Where(it => it.InventoryId == inventory.Id).ToListAsync();
        return Ok(new { inventory, items });
    }

    // ─────────────────────────────────────────────────────────────
    //  BANK — comptes et transactions (vue admin)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Comptes bancaires d'un character (via AccountAccesses).</summary>
    [HttpGet("characters/{id}/accounts")]
    public async Task<IActionResult> GetCharacterAccounts(string id)
    {
        var accountIds = await _context.AccountAccesses
            .Where(a => a.CharacterId == id)
            .Select(a => a.AccountId)
            .ToListAsync();

        var accounts = await _context.BankAccounts
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.AccountNumber,
                a.AccountName,
                a.AccountType,
                Balance = BankService.FromCents(a.BalanceCents),
                a.IsActive,
                a.CreatedAt,
            })
            .ToListAsync();

        return Ok(accounts);
    }

    /// <summary>Transactions d'un compte (paginé).</summary>
    [HttpGet("accounts/{accountId}/transactions")]
    public async Task<IActionResult> GetAccountTransactions(string accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
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
                t.Status,
                Amount = BankService.FromCents(t.AmountCents),
                t.FromAccountId,
                t.ToAccountId,
                t.InitiatorCharacterId,
                t.AtmId,
                t.Comment,
                t.CreatedAt,
            })
            .ToListAsync();

        return Ok(transactions);
    }

    // ─────────────────────────────────────────────────────────────
    //  DASHBOARD — compteurs globaux
    // ─────────────────────────────────────────────────────────────

    /// <summary>Compteurs globaux pour le dashboard admin.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers       = await _context.Users.CountAsync();
        var totalCharacters  = await _context.Characters.CountAsync();
        var totalBans        = await _context.Bans.CountAsync();
        var totalWhitelists  = await _context.Whitelists.CountAsync();
        var totalWarns       = await _context.Warns.CountAsync();
        var totalAccounts    = await _context.BankAccounts.CountAsync();
        var totalTransactions= await _context.Transactions.CountAsync();
        var totalItems       = await _context.Items.CountAsync();

        var totalMoneyCents = await _context.BankAccounts
            .Where(a => a.IsActive)
            .SumAsync(a => a.BalanceCents);

        return Ok(new
        {
            users           = totalUsers,
            characters      = totalCharacters,
            bans            = totalBans,
            whitelists      = totalWhitelists,
            warns           = totalWarns,
            accounts        = totalAccounts,
            transactions    = totalTransactions,
            items           = totalItems,
            totalMoney      = BankService.FromCents(totalMoneyCents),
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  MODERATION — listes (lecture seule, les writes existent déjà dans AdminController)
    // ─────────────────────────────────────────────────────────────

    [HttpGet("warns")]
    public async Task<IActionResult> GetWarns()
    {
        return Ok(await _context.Warns.ToListAsync());
    }

    // ─────────────────────────────────────────────────────────────
    //  POSITIONS — toutes les positions des characters (pour la map)
    // ─────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────
    //  GLOBAL LISTS — tous les items et toutes les transactions
    // ─────────────────────────────────────────────────────────────

    /// <summary>Tous les items du serveur, enrichis du propriétaire (character + steamId).</summary>
    [HttpGet("items")]
    public async Task<IActionResult> GetAllItems([FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var items = await _context.Items
            .OrderBy(i => i.InventoryId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var inventoryIds = items.Select(i => i.InventoryId).Distinct().ToList();
        var inventories  = await _context.Inventories
            .Where(inv => inventoryIds.Contains(inv.Id))
            .ToDictionaryAsync(inv => inv.Id);

        var characterIds = inventories.Values.Select(inv => inv.OwnerId).Distinct().ToList();
        var characters   = await _context.Characters
            .Where(c => characterIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var total = await _context.Items.CountAsync();

        var result = items.Select(i =>
        {
            inventories.TryGetValue(i.InventoryId, out var inv);
            Character? owner = null;
            if (inv != null) characters.TryGetValue(inv.OwnerId, out owner);

            return new
            {
                id             = i.Id,
                itemGameId     = i.ItemGameId,
                count          = i.Count,
                mass           = i.Mass,
                line           = i.Line,
                collum         = i.Collum,
                metadata       = i.Metadata,
                inventoryId    = i.InventoryId,
                characterId    = inv?.OwnerId,
                characterName  = owner != null ? $"{owner.FirstName} {owner.LastName}" : null,
                ownerSteamId   = owner?.OwnerId,
            };
        });

        return Ok(new { total, page, pageSize, items = result });
    }

    /// <summary>Toutes les transactions du serveur (paginé).</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetAllTransactions([FromQuery] int page = 1, [FromQuery] int pageSize = 200)
    {
        var total = await _context.Transactions.CountAsync();

        var transactions = await _context.Transactions
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.Status,
                Amount = BankService.FromCents(t.AmountCents),
                t.FromAccountId,
                t.ToAccountId,
                t.InitiatorCharacterId,
                t.AtmId,
                t.Comment,
                t.CreatedAt,
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, transactions });
    }

    [HttpGet("positions")]
    public async Task<IActionResult> GetAllPositions()
    {
        var positions = await _context.CharacterPositions.ToListAsync();
        var ids       = positions.Select(p => p.CharacterId).ToList();
        var characters= await _context.Characters
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);

        var result = positions.Select(p =>
        {
            characters.TryGetValue(p.CharacterId, out var c);
            return new
            {
                characterId = p.CharacterId,
                firstName   = c?.FirstName,
                lastName    = c?.LastName,
                ownerId     = c?.OwnerId,
                isSelected  = c?.IsSelected ?? false,
                x = p.X, y = p.Y, z = p.Z,
            };
        });

        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  AUDIT / LOGS — sessions, chat, actions admin
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Liste des sessions de jeu, paginée et filtrable.
    /// Filtres : from, to (DateTime ISO), steamId, activeOnly.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? steamId,
        [FromQuery] bool activeOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);

        var q = _context.Sessions.AsQueryable();
        if (from.HasValue)     q = q.Where(s => s.JoinedAt >= from.Value);
        if (to.HasValue)       q = q.Where(s => s.JoinedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(steamId)) q = q.Where(s => s.SteamId == steamId);
        if (activeOnly)        q = q.Where(s => s.LeftAt == null);

        var total = await q.CountAsync();
        var rows  = await q
            .OrderByDescending(s => s.JoinedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, sessions = rows });
    }

    /// <summary>Sessions actuellement ouvertes (LeftAt = null).</summary>
    [HttpGet("sessions/active")]
    public async Task<IActionResult> GetActiveSessions()
    {
        var rows = await _context.Sessions
            .Where(s => s.LeftAt == null)
            .OrderByDescending(s => s.JoinedAt)
            .ToListAsync();
        return Ok(rows);
    }

    /// <summary>
    /// Temps de jeu agrégé par joueur sur la fenêtre demandée.
    /// Inclut les sessions terminées (DurationSeconds) ET les sessions actives
    /// (calculé en live: now - JoinedAt).
    /// </summary>
    [HttpGet("sessions/playtime")]
    public async Task<IActionResult> GetPlaytime(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? steamId)
    {
        var q = _context.Sessions.AsQueryable();
        if (from.HasValue)     q = q.Where(s => s.JoinedAt >= from.Value);
        if (to.HasValue)       q = q.Where(s => s.JoinedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(steamId)) q = q.Where(s => s.SteamId == steamId);

        var sessions = await q.ToListAsync();
        var now = DateTime.UtcNow;

        var grouped = sessions
            .GroupBy(s => new { s.SteamId, s.DisplayName })
            .Select(g => new
            {
                steamId      = g.Key.SteamId,
                displayName  = g.Key.DisplayName,
                sessionCount = g.Count(),
                totalSeconds = g.Sum(s => s.DurationSeconds ?? (int)Math.Max(0, (now - s.JoinedAt).TotalSeconds)),
                firstJoinAt  = g.Min(s => s.JoinedAt),
                lastJoinAt   = g.Max(s => s.JoinedAt),
            })
            .OrderByDescending(g => g.totalSeconds)
            .ToList();

        return Ok(grouped);
    }

    /// <summary>
    /// Messages chat, paginés et filtrables. `search` fait un Contains côté DB sur Message.
    /// </summary>
    [HttpGet("chat")]
    public async Task<IActionResult> GetChat(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? steamId,
        [FromQuery] string? channel,
        [FromQuery] string? search,
        [FromQuery] bool? excludeCommands,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);

        var q = _context.ChatLogs.AsQueryable();
        if (from.HasValue)    q = q.Where(c => c.SentAt >= from.Value);
        if (to.HasValue)      q = q.Where(c => c.SentAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(steamId)) q = q.Where(c => c.SteamId == steamId);
        if (!string.IsNullOrWhiteSpace(channel)) q = q.Where(c => c.Channel == channel);
        if (!string.IsNullOrWhiteSpace(search))  q = q.Where(c => c.Message.Contains(search));
        if (excludeCommands == true) q = q.Where(c => !c.IsCommand);

        var total = await q.CountAsync();
        var rows  = await q
            .OrderByDescending(c => c.SentAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, messages = rows });
    }

    /// <summary>
    /// Historique des transferts d'inventaire pour audit anti-duplication.
    /// Filtrable par joueur, character, item, action — utilisé notamment dans
    /// la fiche personnage pour avoir tout l'historique d'items.
    /// </summary>
    [HttpGet("inventory")]
    public async Task<IActionResult> GetInventoryLogs(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? steamId,
        [FromQuery] string? characterId,
        [FromQuery] string? itemGameId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);

        var q = _context.InventoryLogs.AsQueryable();
        if (from.HasValue)    q = q.Where(i => i.At >= from.Value);
        if (to.HasValue)      q = q.Where(i => i.At <= to.Value);
        if (!string.IsNullOrWhiteSpace(steamId))     q = q.Where(i => i.ActorSteamId == steamId);
        if (!string.IsNullOrWhiteSpace(characterId)) q = q.Where(i => i.CharacterId  == characterId);
        if (!string.IsNullOrWhiteSpace(itemGameId))  q = q.Where(i => i.ItemGameId   == itemGameId);
        if (!string.IsNullOrWhiteSpace(action))      q = q.Where(i => i.Action       == action);

        var total = await q.CountAsync();
        var rows  = await q
            .OrderByDescending(i => i.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, logs = rows });
    }

    /// <summary>
    /// Actions admin (web ou in-game), paginées et filtrables.
    /// </summary>
    [HttpGet("admin-actions")]
    public async Task<IActionResult> GetAdminActions(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? adminSteamId,
        [FromQuery] string? targetSteamId,
        [FromQuery] string? action,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        page     = Math.Max(1, page);

        var q = _context.AdminActionLogs.AsQueryable();
        if (from.HasValue)    q = q.Where(a => a.At >= from.Value);
        if (to.HasValue)      q = q.Where(a => a.At <= to.Value);
        if (!string.IsNullOrWhiteSpace(adminSteamId))  q = q.Where(a => a.AdminSteamId  == adminSteamId);
        if (!string.IsNullOrWhiteSpace(targetSteamId)) q = q.Where(a => a.TargetSteamId == targetSteamId);
        if (!string.IsNullOrWhiteSpace(action))        q = q.Where(a => a.Action == action);
        if (!string.IsNullOrWhiteSpace(source))        q = q.Where(a => a.Source == source);

        var total = await q.CountAsync();
        var rows  = await q
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        return Ok(new { total, page, pageSize, actions = rows });
    }
}
