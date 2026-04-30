
namespace OpenFramework.Database.DTO;

public class InventoryDTO : ITableDTO
{
	[TableProperty( true )]
	public Guid Id { get; set; }

	[TableProperty( true, true )]
	public ulong SteamId { get; set; }

	[TableProperty]
	public List<string> Items { get; set; } = new();
}
