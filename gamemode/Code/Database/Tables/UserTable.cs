using System.Net.Http;
using OpenFramework.Database;
using OpenFramework.Database.DTO;

namespace OpenFramework.Database.Tables;

/// <summary>
/// Represents a table for managing user data in the database.
/// </summary>
/// <remarks>
/// This class extends the <see cref="Table{T}"/> class, specifically for handling <see cref="UserDTO"/> objects.
/// It provides functionality to load, insert, find, and delete user entries in the associated database table.
/// </remarks>
public class UserTable : Table<UserDTO>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="UserTable"/> class.
	/// </summary>
	public UserTable() : base( "table_users", "users" )
	{
		// Additional initialization can be performed here if necessary.
	}
}
