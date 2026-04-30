
namespace OpenFramework.Database.DTO;

public class JobDTO : ITableDTO
{
	[TableProperty( true )]
	public Guid Id { get; set; }

	[TableProperty( true, true )]
	public string Jobname { get; set; }

	[TableProperty]
	public int Capital { get; set; }
}
