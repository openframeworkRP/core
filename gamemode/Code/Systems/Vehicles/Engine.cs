using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Simulates the engine of a vehicle, ported from lodzero's VehicleEngine.
/// Uses a Bezier torque curve (s&box Curve) instead of formula-based torque.
/// Contains angular velocity simulation with friction model, clutch, gearbox, and differential.
/// Can load values from an EnginePreset GameResource for easy reuse across vehicles.
/// </summary>
[Category( "Vehicles" )]
[Title( "Engine" )]
[Icon( "settings" )]
public sealed class Engine : Component
{
	// ── Preset ────────────────────────────────────────────────────────────────

	/// <summary>Optional engine preset. When assigned, click "Apply Preset" to copy its values.</summary>
	[Property, Group( "Preset" )] public EnginePreset Preset { get; set; }

	/// <summary>Copies all values from the assigned preset into this engine's properties.</summary>
	[Button( "Apply Preset" )]
	public void ApplyPreset()
	{
		if ( Preset == null )
		{
			Log.Warning( "Engine: No preset assigned." );
			return;
		}

		HorsePower = Preset.HorsePower;
		MaxRPM = Preset.RpmMax;
		IdleRPM = Preset.RpmIdle;
		PeakHorsepowerRPM = Preset.PeakHorsepowerRPM;
		FrictionCoef = Preset.FrictionCoef;
		Inertia = Preset.Inertia;
		StartFriction = Preset.StartFriction;
		DifferentialRatio = Preset.DifferentialRatio;
		ClutchCapacity = Preset.ClutchCapacity;
		ClutchStiffness = Preset.ClutchStiffness;
		ClutchDamping = Preset.ClutchDamping;
		ShiftUpRPM = Preset.ShiftUpRPM;
		ShiftDownRPM = Preset.ShiftDownRPM;
		Drivetrain = Preset.Drivetrain;
		Diff = Preset.Diff;

		if ( Preset.TorqueCurve.Frames != null && Preset.TorqueCurve.Frames.Length > 0 )
			TorqueCurve = Preset.TorqueCurve;

		// Apply fuel settings
		FuelType = Preset.FuelType;
		FuelCapacity = Preset.FuelCapacity;
		FuelConsumptionRate = Preset.FuelConsumptionRate;
		FuelLevel = Preset.FuelCapacity;

		// Apply gearbox preset if linked
		if ( Preset.GearboxPreset != null )
		{
			var gearbox = Components.GetInAncestorsOrSelf<Gearbox>();
			if ( gearbox.IsValid() )
			{
				gearbox.Preset = Preset.GearboxPreset;
				gearbox.ApplyPreset();
			}
		}

		// Apply forced induction settings
		var fi = Components.GetInAncestorsOrSelf<ForcedInduction>();
		if ( fi.IsValid() )
		{
			if ( Preset.HasForcedInduction )
			{
				fi.Enabled = true;
				fi.Type = Preset.InductionType;
				fi.MaxBoost = Preset.MaxBoost;
				fi.BoostMultiplier = Preset.BoostMultiplier;
				if ( Preset.SpoolLoop != null )
					fi.SpoolLoop = Preset.SpoolLoop;
				if ( Preset.BlowOffValve != null )
					fi.BlowOffValve = Preset.BlowOffValve;
			}
			else
			{
				fi.Enabled = false;
			}
		}

		// Apply sounds to VehicleSound component
		var vehicleSound = Components.GetInAncestorsOrSelf<VehicleSound>();
		if ( vehicleSound.IsValid() )
		{
			if ( Preset.EngineStart != null )
				vehicleSound.EngineStart = Preset.EngineStart;
			if ( Preset.EngineStop != null )
				vehicleSound.EngineStop = Preset.EngineStop;
		}

		// Re-initialize subsystems with new values
		ResetEngine();

		Log.Info( $"Engine: Applied preset '{Preset.DisplayName}'" );
	}

	// ── Drivetrain config (lodzero puts these on Engine) ──────────────────────

	/// <summary>Which axles receive motor torque.</summary>
	[Property, Group( "Drivetrain" )] public DrivetrainType Drivetrain { get; set; } = DrivetrainType.FWD;

	/// <summary>Differential type for torque split between left/right wheels.</summary>
	[Property, Group( "Drivetrain" )] public DiffType Diff { get; set; } = DiffType.LSD;

	// ── Engine specs ──────────────────────────────────────────────────────────

	/// <summary>Bezier torque curve. X = normalized RPM (0..1), Y = torque factor (0..1). Evaluated per-frame.</summary>
	[Property, Group( "Torque" )] public Curve TorqueCurve { get; set; }

	/// <summary>Peak horsepower output.</summary>
	[Property, Group( "Torque" )] public float HorsePower { get; set; } = 190.0f;

	/// <summary>Multiplicateur de puissance (1.0 = 100%). Réduit par l'usure kilométrique.</summary>
	public float PowerMultiplier { get; set; } = 1.0f;

	/// <summary>RPM at which peak horsepower occurs. Used to compute MaxTorque.</summary>
	[Property, Group( "RPM" )] public float PeakHorsepowerRPM { get; set; } = 7000.0f;

	/// <summary>Absolute maximum RPM — rev limiter kicks in here.</summary>
	[Property, Group( "RPM" )] public float MaxRPM { get; set; } = 7000.0f;

	/// <summary>Minimum RPM when the engine is running (idle).</summary>
	[Property, Group( "RPM" )] public float IdleRPM { get; set; } = 900.0f;

	/// <summary>RPM-proportional friction coefficient. Higher = more engine braking at high RPM.</summary>
	[Property, Group( "Friction" )] public float FrictionCoef { get; set; } = 0.007f;

	/// <summary>Rotational inertia of the crankshaft/flywheel assembly.</summary>
	[Property, Group( "Friction" )] public float Inertia { get; set; } = 0.12f;

	/// <summary>Static friction torque (constant resistance even at 0 RPM).</summary>
	[Property, Group( "Friction" )] public float StartFriction { get; set; } = 10.0f;

	// ── Differential ──────────────────────────────────────────────────────────

	/// <summary>Final drive (differential) ratio.</summary>
	[Property, Group( "Differential" )] public float DifferentialRatio { get; set; } = 3.2f;

	// ── Clutch ────────────────────────────────────────────────────────────────

	/// <summary>Clutch torque capacity multiplier over engine max torque.</summary>
	[Property, Group( "Clutch" )] public float ClutchCapacity { get; set; } = 1.3f;

	/// <summary>Clutch spring strength — how aggressively torque is transferred based on slip.</summary>
	[Property, Group( "Clutch" )] public float ClutchStiffness { get; set; } = 20.0f;

	/// <summary>Clutch damping factor (0..1). Higher = smoother torque transitions.</summary>
	[Property, Group( "Clutch" )] public float ClutchDamping { get; set; } = 0.8f;

	// ── Fuel ──────────────────────────────────────────────────────────────────

	/// <summary>Fuel type (Petrol, Diesel, Electric, etc.).</summary>
	[Property, Group( "Fuel" )] public VehicleFuelType FuelType { get; set; } = VehicleFuelType.Petrol;

	/// <summary>Tank capacity in litres.</summary>
	[Property, Group( "Fuel" )] public float FuelCapacity { get; set; } = 50f;

	/// <summary>Consumption rate in litres/hour at max RPM + full throttle.</summary>
	[Property, Group( "Fuel" )] public float FuelConsumptionRate { get; set; } = 12f;

	/// <summary>Reserve threshold (fraction of capacity, e.g. 0.15 = 15%).</summary>
	[Property, Group( "Fuel" )] public float FuelReserveThreshold { get; set; } = 0.15f;

	/// <summary>Sputtering threshold (fraction of capacity, e.g. 0.03 = 3%).</summary>
	[Property, Group( "Fuel" )] public float FuelSputterThreshold { get; set; } = 0.03f;

	/// <summary>Current fuel level in litres. Synced for HUD.</summary>
	[Sync( SyncFlags.FromHost )] public float FuelLevel { get; set; } = 50f;

	/// <summary>True when fuel is below reserve threshold.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsLowFuel { get; private set; }

	/// <summary>True when completely out of fuel — engine stalls.</summary>
	[Sync( SyncFlags.FromHost )] public bool IsOutOfFuel { get; private set; }

	/// <summary>True when engine is sputtering (nearly empty tank).</summary>
	[Sync( SyncFlags.FromHost )] public bool IsSputtering { get; private set; }

	// Sputtering runtime state
	private bool _sputterCutoff;
	private float _sputterTimer;
	private float _sputterDuration;

	// ── Shift RPM (passed to Gearbox) ─────────────────────────────────────────

	/// <summary>RPM at which the gearbox shifts up (auto transmission).</summary>
	[Property, Group( "Auto Shift" )] public float ShiftUpRPM { get; set; } = 6600.0f;

	/// <summary>RPM at which the gearbox shifts down (auto transmission).</summary>
	[Property, Group( "Auto Shift" )] public float ShiftDownRPM { get; set; } = 3500.0f;

	// ── Runtime state (synced for passengers HUD, sounds, camera) ────────────

	/// <summary>Current engine RPM this frame.</summary>
	[Sync( SyncFlags.FromHost )] public float RPM { get; private set; }

	/// <summary>Current engine crankshaft angular velocity in rad/s.</summary>
	[Sync( SyncFlags.FromHost )] public float AngularVelocity { get; set; } = 100.0f;

	/// <summary>Throttle position set by the vehicle (0 = off, 1 = full throttle). Negative for reverse.</summary>
	[Sync( SyncFlags.FromHost )] public float Throttle { get; set; }

	/// <summary>Computed max torque in ft-lbs: (HP * 5252) / PeakHorsepowerRPM.</summary>
	public float MaxTorque { get; private set; }

	// ── Subsystems ────────────────────────────────────────────────────────────

	/// <summary>The differential subsystem (struct). Splits torque left/right.</summary>
	public Differential Differential;

	/// <summary>The clutch subsystem (struct). Transfers torque between engine and gearbox.</summary>
	public Clutch Clutch;

	/// <summary>Reference to the Gearbox component on the same vehicle.</summary>
	public Gearbox Gearbox { get; private set; }

	// ── Rev limiter ───────────────────────────────────────────────────────────

	private bool _isRevLimiting;
	private float _revLimiterCooldown = 0.05f;
	private float _revLimiterTimer;

	// ── Constants ─────────────────────────────────────────────────────────────

	private static readonly float RPMToRad = (MathF.PI * 2.0f) / 60.0f;
	private static readonly float RadToRPM = 1.0f / RPMToRad;

	// ── Engine effective torque (for debug) ────────────────────────────────────

	private float _engineEffectiveTorque;
	private float _loadTorque;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Gearbox = Components.GetInAncestorsOrSelf<Gearbox>();
		ResetEngine();
	}

	/// <summary>
	/// Resets the engine to its initial state. Recomputes MaxTorque and recreates subsystems.
	/// </summary>
	public void ResetEngine()
	{
		RPM = IdleRPM;
		AngularVelocity = IdleRPM * RPMToRad;
		_engineEffectiveTorque = 0.0f;
		_loadTorque = 0.0f;
		_isRevLimiting = false;
		_revLimiterTimer = 0.0f;

		// HP * 5252 / PeakRPM = peak torque in ft-lbs (lodzero's formula)
		MaxTorque = (HorsePower * 5252.0f) / PeakHorsepowerRPM;

		Differential = new Differential( Diff, DifferentialRatio );
		Clutch = new Clutch( MaxTorque, ClutchCapacity, ClutchStiffness, ClutchDamping );

		// Sync shift RPM to gearbox if present
		if ( Gearbox.IsValid() )
		{
			Gearbox.ShiftUpRpm = ShiftUpRPM;
			Gearbox.ShiftDownRpm = ShiftDownRPM;
		}
	}

	// ── Public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Updates the engine simulation for one physics tick.
	/// Ported from lodzero's VehicleEngine.UpdateEngine.
	/// </summary>
	/// <param name="deltaTime">Physics timestep.</param>
	/// <param name="throttle">Signed throttle input (-1 to 1). Positive = forward, negative = reverse.</param>
	/// <param name="loadTorque">Load torque from the clutch/drivetrain acting against the engine.</param>
	/// <param name="vehicleSpeed">Vehicle speed (for gearbox auto-shift logic).</param>
	/// <param name="brake">Brake input (0 to 1).</param>
	public void UpdateEngine( float deltaTime, float throttle, float loadTorque, float vehicleSpeed, float brake )
	{
		_loadTorque = loadTorque;

		// ── Fuel consumption (host-only to avoid client desync) ──────────
		if ( FuelType != VehicleFuelType.Electric && Networking.IsHost )
		{
			float normalizedRpm01 = RPM / MathF.Max( MaxRPM, 1f );
			float throttleLoad = MathF.Max( MathF.Abs( throttle ), 0.05f );
			const float idleFactor = 0.1f;
			float consumption = FuelConsumptionRate * (idleFactor + (1f - idleFactor) * normalizedRpm01 * throttleLoad);
			FuelLevel -= consumption * (deltaTime / 3600f);
			FuelLevel = MathF.Max( FuelLevel, 0f );

			IsLowFuel = FuelLevel <= FuelCapacity * FuelReserveThreshold;
			IsOutOfFuel = FuelLevel <= 0f;

			// Sputtering when below sputter threshold but not yet empty
			float sputterLevel = FuelCapacity * FuelSputterThreshold;
			if ( FuelLevel > 0f && FuelLevel <= sputterLevel )
			{
				IsSputtering = true;
				float severity = 1f - (FuelLevel / sputterLevel);
				_sputterTimer -= deltaTime;
				if ( _sputterTimer <= 0f )
				{
					_sputterCutoff = !_sputterCutoff;
					if ( _sputterCutoff )
						_sputterDuration = 0.05f + Random.Shared.NextSingle() * 0.15f;
					else
						_sputterDuration = 0.08f + Random.Shared.NextSingle() * 0.25f * (1f - severity);
					_sputterTimer = _sputterDuration;
				}
			}
			else
			{
				IsSputtering = false;
				_sputterCutoff = false;
			}
		}

		// Fuel throttle override (host-only, synced states drive client HUD)
		if ( Networking.IsHost )
		{
			if ( IsOutOfFuel && FuelType != VehicleFuelType.Electric )
			{
				throttle = 0f;
			}
			else if ( IsSputtering && _sputterCutoff )
			{
				throttle *= 0.05f;
			}
		}

		// Convert throttle into direction and magnitude
		float throttleDir = MathF.Sign( throttle );
		float throttleAmt = MathF.Abs( throttle );

		// Evaluate torque from Bezier torque curve
		float normalizedRPM = RPM.MapRange( 0.0f, MaxRPM, 0.0f, 1.0f );
		float torqueFactor = TorqueCurve.Evaluate( normalizedRPM );
		float maxTorque = torqueFactor * MaxTorque * PowerMultiplier;

		// Rev limiter logic
		if ( RPM >= MaxRPM )
		{
			_isRevLimiting = true;
			_revLimiterTimer = _revLimiterCooldown;
		}
		else if ( _revLimiterTimer > 0.0f )
		{
			_revLimiterTimer -= deltaTime;
		}
		else
		{
			_isRevLimiting = false;
		}

		// Reduce throttle during gear shifts — engine still has inertia,
		// but less power is applied (driver lifts off slightly during shift)
		float effectiveThrottle = throttleAmt;
		if ( Gearbox.IsValid() && Gearbox.IsShifting )
		{
			effectiveThrottle *= 0.3f;
		}

		// Throttle torque (cut when rev limiting)
		float throttleTorque = 0.0f;
		if ( !_isRevLimiting )
		{
			throttleTorque = maxTorque * effectiveThrottle * throttleDir;
		}

		// Friction torque always resists rotation
		float friction = StartFriction + (RPM * FrictionCoef);
		float frictionTorque = -MathF.Sign( AngularVelocity ) * friction;

		// Total engine torque
		float engineTorque = throttleTorque + frictionTorque;
		_engineEffectiveTorque = engineTorque;

		// Angular acceleration = net torque / inertia
		float acceleration = (_engineEffectiveTorque - loadTorque) / Inertia;

		float min = IdleRPM * RPMToRad;
		float max = MaxRPM * RPMToRad;
		AngularVelocity = MathX.Clamp( AngularVelocity + (acceleration * deltaTime), min, max );
		RPM = AngularVelocity * RadToRPM;
	}

	/// <summary>
	/// Returns the current effective engine torque output for debug/display purposes.
	/// </summary>
	public float GetEffectiveTorque()
	{
		return _engineEffectiveTorque;
	}

	/// <summary>
	/// Returns the current load torque (from drivetrain) for debug/display purposes.
	/// </summary>
	public float GetLoadTorque()
	{
		return _loadTorque;
	}
}
