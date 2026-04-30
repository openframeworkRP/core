using System.Net.Http;
using OpenFramework.Database;
using OpenFramework.Database.DTO;

namespace OpenFramework.Database.Tables;

/// <summary>
/// Represents a table for managing HUD settings data in the database.
/// </summary>
public class HudSettingsTable : Table<HudSettingsDTO>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="HudSettingsTable"/> class.
	/// </summary>
	public HudSettingsTable() : base( "table_hud_settings", "hud_settings" )
	{
		// Additional initialization can be performed here if necessary.
	}
}
