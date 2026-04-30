namespace OpenFramework.Api.Models;

public class InventoryItem
{
    public string Id { get; set; }
    public string InventoryId { get; set; }
    public string ItemGameId { get; set; }
    public float Mass { get; set; }
    public int Count { get; set; } // si = 1 alors unique.
    public Dictionary<string, string> Metadata { get; set; } = new(); // vide si rien dedans.  
    public int Line { get; set;  }
    public int Collum { get; set; }
}