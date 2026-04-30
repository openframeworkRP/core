using OpenFramework.Database;
using OpenFramework.Database.DTO;

namespace OpenFramework.Database.Tables;

/// <summary>
/// Représente la table des métiers dans la base de données.
/// </summary>
public class JobTable : Table<JobDTO>
{
	/// <summary>
	/// Initialise une nouvelle instance de la table des jobs.
	/// </summary>
	public JobTable() : base( "table_jobs", "jobs" )
	{
		// Code d'initialisation spécifique aux jobs si besoin.
	}
}
