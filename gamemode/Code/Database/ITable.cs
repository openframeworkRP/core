using System.Threading.Tasks;

namespace OpenFramework.Database;

/// <summary>
/// Represents a general table interface for managing rows of data.
/// </summary>
public interface ITable
{
	/// <summary>
	/// Gets the name of the table.
	/// </summary>
	string Name { get; }

	/// <summary>
	/// Gets the endpoint URL for the API associated with this table.
	/// </summary>
	string EndPoint { get; }

	/// <summary>
	/// Gets the count of rows currently in the table.
	/// </summary>
	int Count { get; }

	/// <summary>
	/// Gets the count of rows currently in the table.
	/// </summary>
	string TypeName { get; }

	/// <summary>
	/// Fetches all rows from the API.
	/// </summary>
	/// <returns>A task representing the asynchronous operation.</returns>
	Task Load();

	/// <summary>
	/// Saves the data of this table.
	/// </summary>
	Task Save();

	// New method to get rows as IEnumerable
	IEnumerable<object> GetAllRows();

	// New method to get properties for a specific row
	Dictionary<string, object> GetRowProperties( object row );

	/// <summary>
	/// Recherche une ligne par une valeur indexable (ID ou autre).
	/// </summary>
	/// <param name="value">Valeur à chercher (souvent un Guid).</param>
	/// <param name="row">Ligne trouvée, ou null.</param>
	/// <returns>True si une ligne correspondante a été trouvée.</returns>
	bool FindRowByIndexValue( object value, out object row );
}

/// <summary>
/// Represents a generic table interface for managing rows of data of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the rows in the table, which must implement <see cref="ITableDTO"/>.</typeparam>
public interface ITable<T> : ITable where T : ITableDTO
{
	/// <summary>
	/// Inserts a new row into the table.
	/// </summary>
	void InsertRow( T row );

	/// <summary>
	/// Finds a row by its unique identifier.
	/// </summary>
	T FindRowByIndexValue( object value );

	/// <summary>
	/// Finds a row by its unique identifier.
	/// </summary>
	bool FindRowByIndexValue( object value, out T row );

	/// <summary>
	/// Deletes a row by its ID or other key.
	/// </summary>
	void DeleteRow( T row );

	/// <summary>
	/// Check if contains row
	/// </summary>
	/// <param name="row"> Row entry</param>
	/// <returns></returns>
	bool ContainsRow( T row );
}
