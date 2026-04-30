using System.Net.Http;
using OpenFramework.Database;
using OpenFramework.Database.DTO;

namespace OpenFramework.Database.Tables;

/// <summary>
/// Represents a table for managing inventory data in the database.
/// </summary>
/// <remarks>
/// This class extends the <see cref="Table{T}"/> class, specifically for handling <see cref="InventoryDTO"/> objects.
/// It provides functionality to load, insert, find, and delete inventory entries in the associated database table.
/// </remarks>
public class InventoryTable : Table<InventoryDTO>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InventoryTable"/> class.
	/// </summary>
	public InventoryTable() : base( "table_inventory", "inventory" )
	{
		// Additional initialization can be performed here if necessary.
	}
}
