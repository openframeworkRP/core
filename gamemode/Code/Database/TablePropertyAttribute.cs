namespace OpenFramework.Database;

/// <summary>
/// Represents an attribute to mark a property as part of a table schema,
/// with optional indexing and uniqueness constraints.
/// </summary>
[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
public class TablePropertyAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether this property is an index.
	/// If true, this property can be used for searching values efficiently.
	/// </summary>
	public bool IsIndex { get; set; } = false;

	/// <summary>
	/// Gets or sets a value indicating whether this property should have unique values across rows.
	/// If true, this property enforces uniqueness.
	/// </summary>
	public bool IsUnique { get; set; } = false;

	/// <summary>
	/// Initializes a new instance of the <see cref="TablePropertyAttribute"/> class.
	/// </summary>
	/// <param name="name">The name of the property in the table schema.</param>
	/// <param name="isIndex">Indicates if the property is an index (optional, default is false).</param>
	/// <param name="isUnique">Indicates if the property enforces unique values (optional, default is false).</param>
	public TablePropertyAttribute( bool isIndex = false, bool isUnique = false )
	{
		IsIndex = isIndex;
		IsUnique = isUnique;
	}
}
