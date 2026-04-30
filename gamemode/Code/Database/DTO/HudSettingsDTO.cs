
namespace OpenFramework.Database.DTO;

public class HudSettingsDTO : ITableDTO
{
	public Guid Id { get; set; }

	[TableProperty] public bool ShowHud { get; set; } = true;
	[TableProperty] public bool ShowHealth { get; set; } = true;
	[TableProperty] public bool ShowStamina { get; set; } = true;
	[TableProperty] public bool ShowHunger { get; set; } = true;
	[TableProperty] public bool ShowThirst { get; set; } = true;
	[TableProperty] public bool ShowOxygen { get; set; } = true;
	[TableProperty] public bool ShowTime { get; set; } = true;
	[TableProperty] public bool ShowLocation { get; set; } = true;
	[TableProperty] public bool ShowCompass { get; set; } = true;
	[TableProperty] public string Style { get; set; } = "Minimal";
	[TableProperty] public string AccentColor { get; set; } = "Light";
}
