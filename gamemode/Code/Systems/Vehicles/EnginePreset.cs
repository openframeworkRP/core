namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Predefined engine configuration using lodzero's torque curve model.
/// Create presets for different engine types and assign them to vehicles.
/// </summary>
[AssetType( Name = "Engine Preset", Extension = "engine" )]
public class EnginePreset : GameResource
{
	[Property, Group( "Info" )] public string DisplayName { get; set; } = "Custom Engine";

	// ── Drivetrain ────────────────────────────────────────────────────────────

	/// <summary>Which axles receive motor torque.</summary>
	[Property, Group( "Drivetrain" )] public DrivetrainType Drivetrain { get; set; } = DrivetrainType.FWD;

	/// <summary>Differential type for torque split between left/right wheels.</summary>
	[Property, Group( "Drivetrain" )] public DiffType Diff { get; set; } = DiffType.LSD;

	// ── Torque & Power ────────────────────────────────────────────────────────

	/// <summary>Bezier torque curve. X = normalized RPM (0..1), Y = torque factor (0..1).</summary>
	[Property, Group( "Torque" )] public Curve TorqueCurve { get; set; }

	/// <summary>Peak horsepower output.</summary>
	[Property, Group( "Torque" )] public float HorsePower { get; set; } = 190.0f;

	/// <summary>RPM at which peak horsepower occurs. Used to compute MaxTorque: (HP * 5252) / PeakRPM.</summary>
	[Property, Group( "Torque" )] public float PeakHorsepowerRPM { get; set; } = 7000.0f;

	// ── RPM ───────────────────────────────────────────────────────────────────

	/// <summary>Engine idle RPM.</summary>
	[Property, Group( "RPM" )] public float RpmIdle { get; set; } = 900.0f;

	/// <summary>Absolute maximum RPM — rev limiter kicks in here.</summary>
	[Property, Group( "RPM" )] public float RpmMax { get; set; } = 7000.0f;

	// ── Friction ──────────────────────────────────────────────────────────────

	/// <summary>RPM-proportional friction coefficient.</summary>
	[Property, Group( "Friction" )] public float FrictionCoef { get; set; } = 0.007f;

	/// <summary>Rotational inertia of the crankshaft/flywheel assembly.</summary>
	[Property, Group( "Friction" )] public float Inertia { get; set; } = 0.12f;

	/// <summary>Static friction torque (constant resistance).</summary>
	[Property, Group( "Friction" )] public float StartFriction { get; set; } = 10.0f;

	// ── Differential ──────────────────────────────────────────────────────────

	/// <summary>Final drive (differential) ratio.</summary>
	[Property, Group( "Differential" )] public float DifferentialRatio { get; set; } = 3.2f;

	// ── Clutch ────────────────────────────────────────────────────────────────

	/// <summary>Clutch torque capacity multiplier over engine max torque.</summary>
	[Property, Group( "Clutch" )] public float ClutchCapacity { get; set; } = 1.3f;

	/// <summary>Clutch spring strength.</summary>
	[Property, Group( "Clutch" )] public float ClutchStiffness { get; set; } = 20.0f;

	/// <summary>Clutch damping factor (0..1).</summary>
	[Property, Group( "Clutch" )] public float ClutchDamping { get; set; } = 0.8f;

	// ── Auto Shift ────────────────────────────────────────────────────────────

	/// <summary>RPM at which the gearbox shifts up.</summary>
	[Property, Group( "Auto Shift" )] public float ShiftUpRPM { get; set; } = 6600.0f;

	/// <summary>RPM at which the gearbox shifts down.</summary>
	[Property, Group( "Auto Shift" )] public float ShiftDownRPM { get; set; } = 3500.0f;

	// ── Fuel ──────────────────────────────────────────────────────────────────

	/// <summary>Fuel type for this engine.</summary>
	[Property, Group( "Fuel" )] public VehicleFuelType FuelType { get; set; } = VehicleFuelType.Petrol;

	/// <summary>Tank capacity in litres.</summary>
	[Property, Group( "Fuel" )] public float FuelCapacity { get; set; } = 50f;

	/// <summary>Fuel consumption rate in litres/hour at max RPM + full throttle.</summary>
	[Property, Group( "Fuel" )] public float FuelConsumptionRate { get; set; } = 12f;

	// ── Speed ─────────────────────────────────────────────────────────────────

	/// <summary>Top speed in km/h. Engine torque is cut as the vehicle approaches this speed.</summary>
	[Property, Group( "Speed" )] public float TopSpeedKmh { get; set; } = 180f;

	// ── Gearbox ───────────────────────────────────────────────────────────────

	/// <summary>Gearbox preset to apply alongside this engine.</summary>
	[Property, Group( "Gearbox" )] public GearboxPreset GearboxPreset { get; set; }

	// ── Forced Induction ──────────────────────────────────────────────────────

	/// <summary>Whether this engine has forced induction.</summary>
	[Property, Group( "Forced Induction" )] public bool HasForcedInduction { get; set; } = false;

	/// <summary>Type of forced induction.</summary>
	[Property, Group( "Forced Induction" )] public ForcedInduction.InductionType InductionType { get; set; } = ForcedInduction.InductionType.Turbo;

	/// <summary>Maximum boost in bar.</summary>
	[Property, Group( "Forced Induction" )] public float MaxBoost { get; set; } = 0.8f;

	/// <summary>Torque multiplier at full boost.</summary>
	[Property, Group( "Forced Induction" )] public float BoostMultiplier { get; set; } = 0.4f;

	/// <summary>Turbo spool/supercharger whine loop.</summary>
	[Property, Group( "Forced Induction" )] public SoundEvent SpoolLoop { get; set; }

	/// <summary>Blow-off valve sound (turbo only).</summary>
	[Property, Group( "Forced Induction" )] public SoundEvent BlowOffValve { get; set; }

	// ── Sound ─────────────────────────────────────────────────────────────────

	/// <summary>Engine idle loop sound (pitched based on RPM).</summary>
	[Property, Group( "Sound" )] public SoundEvent EngineIdleLoop { get; set; }

	/// <summary>Engine start sound.</summary>
	[Property, Group( "Sound" )] public SoundEvent EngineStart { get; set; }

	/// <summary>Engine stop sound.</summary>
	[Property, Group( "Sound" )] public SoundEvent EngineStop { get; set; }
}
