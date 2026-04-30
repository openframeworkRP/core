namespace OpenFramework.Inventory;

public class ItemDto
{
	public string ContainerId { get; set; }
	public string MetadataId { get; set; }
	public int SlotIndex { get; set; }
	public int Quantity { get; set; }
	public Dictionary<string, string> Attributes { get; set; }
}
