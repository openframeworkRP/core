using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class InventoryService
{
    private readonly OpenFrameworkDbContext _db;
    private readonly CacheService _cache;

    private static readonly TimeSpan InvTtl = TimeSpan.FromMinutes(1);

    public InventoryService(OpenFrameworkDbContext db, CacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task AddItemInActualInventoryAsync(string characterId, InventoryItem item)
    {
        var inventory = await _db.Inventories.FirstOrDefaultAsync(t => t.OwnerId == characterId);
        if (inventory == null) return;

        item.Id = Guid.NewGuid().ToString();
        item.InventoryId = inventory.Id;
        _db.Items.Add(item);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(CacheService.InvKey(characterId));
    }

    public async Task DeleteItemInActualInventoryAsync(string characterId, int line, int collum)
    {
        var inventory = await _db.Inventories.FirstOrDefaultAsync(t => t.OwnerId == characterId);
        if (inventory == null) return;

        var item = await _db.Items.FirstOrDefaultAsync(t =>
            t.InventoryId == inventory.Id && t.Line == line && t.Collum == collum);
        if (item == null) return;

        _db.Items.Remove(item);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(CacheService.InvKey(characterId));
    }

    public async Task UpdateItemInActualInventoryAsync(string characterId, int line, int collum, int newLine, int newCollum)
    {
        var inventory = await _db.Inventories.FirstOrDefaultAsync(t => t.OwnerId == characterId);
        if (inventory == null) return;

        var item = await _db.Items.FirstOrDefaultAsync(t =>
            t.InventoryId == inventory.Id && t.Line == line && t.Collum == collum);
        if (item == null) return;

        item.Line = newLine;
        item.Collum = newCollum;
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(CacheService.InvKey(characterId));
    }

    public async Task<List<InventoryItem>> GetAllItemsAsync(string characterId)
    {
        var cached = await _cache.GetAsync<List<InventoryItem>>(CacheService.InvKey(characterId));
        if (cached != null) return cached;

        var inventory = await _db.Inventories.AsNoTracking().FirstOrDefaultAsync(t => t.OwnerId == characterId);
        if (inventory == null) return [];

        var items = await _db.Items.AsNoTracking().Where(t => t.InventoryId == inventory.Id).ToListAsync();
        await _cache.SetAsync(CacheService.InvKey(characterId), items, InvTtl);
        return items;
    }

    public async Task ClearInventoryAsync(string characterId)
    {
        var inventory = await _db.Inventories.FirstOrDefaultAsync(t => t.OwnerId == characterId);
        if (inventory == null) return;

        var items = await _db.Items.Where(t => t.InventoryId == inventory.Id).ToListAsync();
        _db.Items.RemoveRange(items);
        await _db.SaveChangesAsync();

        await _cache.RemoveAsync(CacheService.InvKey(characterId));
    }
}
