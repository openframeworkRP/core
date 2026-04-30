using System.Text.Json.Serialization;

namespace OpenFramework.Database.DTO;

public class CommandDTO : ITableDTO
{
	public Guid Id { get; set; }

	/// <summary>
	/// Name of the command.
	/// </summary>
	[TableProperty( true, true )]
	public string Name { get; set; }

	/// <summary>
	/// Description of the command.
	/// </summary>
	[TableProperty]
	public string Description { get; set; }

	/// <summary>
	/// Is this command enabled..
	/// </summary>
	[TableProperty]
	public bool Enabled { get; set; }

	[JsonIgnore]
	public string Icon { get; set; }

	[JsonIgnore]
	public string[] Aliases { get; set; }

	[JsonIgnore]
	public Dictionary<string, bool> Arguments { get; set; }
}
