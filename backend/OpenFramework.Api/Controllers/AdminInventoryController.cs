using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.DToS;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/admin/inventory")]
[Authorize(Roles = "GameServer")]
public class AdminInventoryController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;

    public AdminInventoryController(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Donne un item à un personnage.
    /// Requiert un JWT GameServer (POST /api/auth/server-login avec le secret serveur).
    /// </summary>
    [HttpPost("give")]
    public async Task<IActionResult> GiveItem([FromBody] AdminGiveItemDto dto)
    {
        if (string.IsNullOrEmpty(dto.CharacterId) || string.IsNullOrEmpty(dto.ItemGameId))
            return BadRequest(new { success = false, error = "CharacterId et ItemGameId sont requis." });

        var inventory = await _context.Inventories.FirstOrDefaultAsync(i => i.OwnerId == dto.CharacterId);
        if (inventory == null)
            return NotFound(new { success = false, error = "Inventaire introuvable pour ce personnage." });

        var item = new InventoryItem
        {
            Id = Guid.NewGuid().ToString(),
            InventoryId = inventory.Id,
            ItemGameId = dto.ItemGameId,
            Mass = dto.Mass,
            Count = dto.Count,
            Metadata = dto.Metadata ?? new Dictionary<string, string>(),
            Line = dto.Line,
            Collum = dto.Collum
        };

        _context.Items.Add(item);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, itemId = item.Id });
    }

    /// <summary>
    /// Modifie les valeurs d'un item existant (champs partiels : seuls les champs fournis sont mis à jour).
    /// Requiert un JWT GameServer.
    /// </summary>
    [HttpPatch("item/{itemId}")]
    public async Task<IActionResult> ModifyItem(string itemId, [FromBody] AdminModifyItemDto dto)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return NotFound(new { success = false, error = "Item introuvable." });

        if (dto.ItemGameId != null) item.ItemGameId = dto.ItemGameId;
        if (dto.Mass.HasValue)      item.Mass = dto.Mass.Value;
        if (dto.Count.HasValue)     item.Count = dto.Count.Value;
        if (dto.Metadata != null)   item.Metadata = dto.Metadata;
        if (dto.Line.HasValue)      item.Line = dto.Line.Value;
        if (dto.Collum.HasValue)    item.Collum = dto.Collum.Value;

        _context.Items.Update(item);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, item });
    }

    /// <summary>
    /// Supprime un item de l'inventaire d'un personnage.
    /// Requiert un JWT GameServer.
    /// </summary>
    [HttpDelete("item/{itemId}")]
    public async Task<IActionResult> RemoveItem(string itemId)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemId);
        if (item == null)
            return NotFound(new { success = false, error = "Item introuvable." });

        _context.Items.Remove(item);
        await _context.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
