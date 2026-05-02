using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Roles = "GameServer")]
public class AdminInventoryController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;
    private readonly CacheService _cache;

    public AdminInventoryController(OpenFrameworkDbContext context, CacheService cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpPost("give")]
    public async Task<IActionResult> GiveItem([FromBody] GiveItemRequest request)
    {
        if (string.IsNullOrEmpty(request.CharacterId) || string.IsNullOrEmpty(request.ItemGameId))
            return BadRequest(new { success = false, error = "CharacterId et ItemGameId sont requis." });

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.OwnerId == request.CharacterId);
        if (inventory == null)
            return NotFound(new { success = false, error = "Inventaire introuvable pour ce personnage." });

        var item = new InventoryItem
        {
            Id = Guid.NewGuid().ToString(),
            InventoryId = inventory.Id,
            ItemGameId = request.ItemGameId,
            Mass = request.Mass,
            Count = request.Count,
            Metadata = request.Metadata ?? new Dictionary<string, string>(),
            Line = request.Line,
            Collum = request.Collum
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CacheService.InvKey(request.CharacterId));

        return Ok(new { success = true, itemId = item.Id });
    }

    [HttpPatch("item/{itemId}")]
    public async Task<IActionResult> ModifyItem(string itemId, [FromBody] PatchItemRequest request)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return NotFound(new { success = false, error = "Item introuvable." });

        if (request.ItemGameId != null) item.ItemGameId = request.ItemGameId;
        if (request.Mass.HasValue)      item.Mass = request.Mass.Value;
        if (request.Count.HasValue)     item.Count = request.Count.Value;
        if (request.Metadata != null)   item.Metadata = request.Metadata;
        if (request.Line.HasValue)      item.Line = request.Line.Value;
        if (request.Collum.HasValue)    item.Collum = request.Collum.Value;

        _context.Items.Update(item);
        await _context.SaveChangesAsync();

        var inv = await _context.Inventories.FindAsync(item.InventoryId);
        if (inv != null) await _cache.RemoveAsync(CacheService.InvKey(inv.OwnerId));

        return Ok(new { success = true, item });
    }

    [HttpDelete("item/{itemId}")]
    public async Task<IActionResult> RemoveItem(string itemId)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return NotFound(new { success = false, error = "Item introuvable." });

        var inventoryId = item.InventoryId;
        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        var inv = await _context.Inventories.FindAsync(inventoryId);
        if (inv != null) await _cache.RemoveAsync(CacheService.InvKey(inv.OwnerId));

        return Ok(new { success = true });
    }
}
