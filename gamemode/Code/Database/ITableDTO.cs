using OpenFramework.Database;

/// <summary>
/// Represents a Data Transfer Object (DTO) for table rows.
/// </summary>
/// <remarks>
/// This interface serves as a marker interface for all DTOs used with the <see cref="ITable{T}"/> interface.
/// Implementing this interface indicates that the class is a valid data type for table operations.
/// </remarks>
public interface ITableDTO
{
	/// <summary>
	/// Gets or sets the unique identifier for the data transfer object.
	/// </summary>
	Guid Id { get; }
}
