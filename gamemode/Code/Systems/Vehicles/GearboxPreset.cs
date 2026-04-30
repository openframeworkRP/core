namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Predefined gearbox configuration.
/// Create presets for different transmission types and assign them to vehicles.
/// </summary>
[AssetType( Name = "Gearbox Preset", Extension = "gearbox" )]
public class GearboxPreset : GameResource
{
	[Property, Group( "Info" )] public string DisplayName { get; set; } = "Custom Gearbox";

	// ── Ratios ────────────────────────────────────────────────────────────────

	/// <summary>Forward gear ratios from 1st to Nth gear.</summary>
	[Property, Group( "Ratios" )] public List<float> ForwardGearRatios { get; set; } = new() { 3.8f, 2.0f, 1.45f, 1.10f, 0.87f };

	/// <summary>Gear ratio used in reverse (stored as positive, sign applied internally).</summary>
	[Property, Group( "Ratios" )] public float ReverseGearRatio { get; set; } = 3.818f;

	// ── Auto shift ────────────────────────────────────────────────────────────

	/// <summary>RPM at which the gearbox shifts up.</summary>
	[Property, Group( "Auto Shift" )] public float UpshiftRpm { get; set; } = 6600f;

	/// <summary>RPM at which the gearbox shifts down.</summary>
	[Property, Group( "Auto Shift" )] public float DownshiftRpm { get; set; } = 3500f;

	/// <summary>Minimum time in seconds between two consecutive shifts.</summary>
	[Property, Group( "Auto Shift" )] public float ShiftCooldown { get; set; } = 0.1f;

	/// <summary>Time in seconds the gearbox spends disengaged during a shift (clutch disengaged).</summary>
	[Property, Group( "Shift Timing" )] public float ShiftTime { get; set; } = 0.2f;
}
