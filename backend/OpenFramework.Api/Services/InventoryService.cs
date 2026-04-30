using OpenFramework.Api.Data;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class InventoryService
{
    private readonly OpenFrameworkDbContext _openFrameworkDb;

    public InventoryService(OpenFrameworkDbContext openFrameworkDb)
    {
        _openFrameworkDb = openFrameworkDb;
    }

    public void AddItemInActualInventory(string characterId, InventoryItem item)
    {
        var inventory = _openFrameworkDb.Inventories.FirstOrDefault(t => t.OwnerId == characterId);
        if (inventory == null)
        {
            return;
        }
        
        // TODO: Limiter les items par le poids.
        item.Id = Guid.NewGuid().ToString();
        item.InventoryId = inventory.Id;
        _openFrameworkDb.Items.Add(item);
        _openFrameworkDb.SaveChanges();
    }

    public void DeleteItemInActualInventory(string characterId, int line, int collum)
    {
        var inventory = _openFrameworkDb.Inventories.FirstOrDefault(t => t.OwnerId == characterId);
        var item = _openFrameworkDb.Items.FirstOrDefault(t => t.InventoryId == inventory.Id && t.Line == line && t.Collum == collum);
        
        _openFrameworkDb.Items.Remove(item);
        _openFrameworkDb.SaveChanges();
    }

    public void UpdateItemInActualInventory(string characterId ,int line, int collum, int newLine,  int newCollum)
    {
        var inventory = _openFrameworkDb.Inventories.FirstOrDefault(t => t.OwnerId == characterId);
        var item = _openFrameworkDb.Items.FirstOrDefault(t => t.InventoryId == inventory.Id && t.Line == line && t.Collum == collum);
        item.Line = newLine;
        item.Collum = newCollum;
        _openFrameworkDb.Items.Update(item);
        _openFrameworkDb.SaveChanges();
    }

    public List<InventoryItem> GetAllItems(string characterId)
    {
        var inventory = _openFrameworkDb.Inventories.FirstOrDefault(t => t.OwnerId == characterId);
        var items = _openFrameworkDb.Items.Where(t => t.InventoryId == inventory.Id).ToList();
        return items;
    }

    public void ClearInventory(string characterId)
    {
        var inventory = _openFrameworkDb.Inventories.FirstOrDefault(t => t.OwnerId == characterId);
        var items = _openFrameworkDb.Items.Where(t => t.InventoryId == inventory.Id).ToList();

        foreach (var item in items)
        {
            _openFrameworkDb.Items.Remove(item);
        }
        _openFrameworkDb.SaveChanges();
    } 
}