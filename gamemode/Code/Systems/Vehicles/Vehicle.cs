using OpenFramework.Extension;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Vehicles.UI;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

public class OdometerData
{
	public float Km { get; set; }
}

public class FuelData
{
	public float Litres { get; set; }
}

public partial class Vehicle : Component, IRespawnable, IUse, IDescription, IDamageListener
{
	// ── Component References ──────────────────────────────────────────────────
	[Property] [Group( "Components" )] public Rigidbody Rigidbody { get; set; }
	[Property] [Group( "Components" )] public ModelRenderer Model { get; set; }

	/// <summary>An accessor for health component if we have one.</summary>
	[Property]
	public virtual HealthComponent HealthComponent { get; set; }

	/// <summary>What to spawn when we explode?</summary>
	[Property]
	[Group( "Effects" )]
	public GameObject Explosion { get; set; }

	[Property] [Group( "Effects" )] public float FireThreshold { get; set; } = 30f;
	[Property] [Group( "Effects" )] public GameObject Fire { get; set; }

	// ── Drivetrain ────────────────────────────────────────────────────────────
	[Property] [Group( "Vehicle" )] public List<Axle> Axles { get; set; }
	[Property] [Group( "Vehicle" )] public List<PlayerSeat> Seats { get; set; }

	/// <summary>Engine component that computes torque from the RPM + torque curve.</summary>
	[Property] [Group( "Vehicle" )] public Engine Engine { get; set; }

	/// <summary>Gearbox component that holds gear ratios and handles auto-shifting.</summary>
	[Property] [Group( "Vehicle" )] public Gearbox Gearbox { get; set; }

	/// <summary>Optional forced induction (turbo or supercharger). Multiplies engine torque based on boost.</summary>
	[Property] [Group( "Vehicle" )] public ForcedInduction ForcedInduction { get; set; }

	/// <summary>Composants internes (moteur, boîte, pneus, turbo) — usure mécanique.</summary>
	[Property] [Group( "Vehicle" )] public VehicleInternals Internals { get; set; }

	/// <summary>Drivetrain type — read from Engine.Drivetrain at runtime.</summary>
	public DrivetrainType Drivetrain => Engine.IsValid() ? Engine.Drivetrain : DrivetrainType.RWD;

	// ── Physics (lodzero model) ───────────────────────────────────────────────

	/// <summary>Vehicle mass in kg (à vide). Overrides the rigidbody mass.</summary>
	[Property] [Group( "Vehicle" )] public float Mass { get; set; } = 1000f;

	/// <summary>Poids total des items dans les storages véhicule (kg). Mis à jour chaque tick.</summary>
	public float StorageWeight { get; private set; }

	/// <summary>Masse totale effective = Mass + StorageWeight.</summary>
	public float TotalMass => Mass + StorageWeight;

	/// <summary>Custom gravity downforce multiplier. 1.0 = normal gravity feel, higher = more planted.</summary>
	[Property] [Group( "Vehicle" )] public float Downforce { get; set; } = 1.0f;

	/// <summary>Center of mass offset in local space (inches). Use negative Z to lower CoM.</summary>
	[Property] [Group( "Vehicle" )] public Vector3 CenterOfMass { get; set; } = new( 10f, 0f, 8f );

	/// <summary>Inertia tensor scale (per-axis). Controls how resistant the body is to rotation on each axis.</summary>
	[Property] [Group( "Vehicle" )] public Vector3 InertiaTensorScale { get; set; } = new( 1.5f, 1.5f, 2.2f );

	// ── Braking ───────────────────────────────────────────────────────────────

	/// <summary>Maximum brake torque applied to wheels in N-m.</summary>
	[Property] [Group( "Vehicle" )] public float MaxBrakeTorque { get; set; } = 600f;

	/// <summary>Front brake bias (0.0 = all rear, 1.0 = all front). 0.6 = punto default.</summary>
	[Property] [Group( "Vehicle" )] public float BrakeBias { get; set; } = 0.6f;

	/// <summary>Maximum handbrake torque applied to rear wheels in N-m.</summary>
	[Property] [Group( "Vehicle" )] public float MaxHandBrakeTorque { get; set; } = 2000f;

	// ── Input Smoothing ───────────────────────────────────────────────────────

	/// <summary>Smoothing rate for throttle/brake input. Higher = faster response.</summary>
	[Property] [Group( "Vehicle" )] public float ThrottleSpeed { get; set; } = 10.0f;

	// ── Driver Aids ───────────────────────────────────────────────────────────

	/// <summary>Traction control strength (lodzero style). 0 = off, 6.0 = default. Controls how fast TC intervenes.</summary>
	[Property] [Group( "Vehicle" )] public float TractionControl { get; set; } = 8.0f;

	/// <summary>Slip ratio threshold before TC activates.</summary>
	[Property] [Group( "Vehicle" )] public float TcSlipThreshold { get; set; } = 0.5f;

	/// <summary>ABS strength. 0 = off, 10.0 = punto default. Modulates brake torque when wheels lock.</summary>
	[Property] [Group( "Vehicle" )] public float ABS { get; set; } = 10.0f;

	// ── Description ───────────────────────────────────────────────────────────
	[Property] [Group( "Description" )] public string DisplayName { get; set; }

	// ── Identification ────────────────────────────────────────────────────────

	/// <summary>Identifiant unique du véhicule. Généré automatiquement au premier spawn si vide.</summary>
	[Property, Group( "Identification" ), Sync( SyncFlags.FromHost )]
	public Guid VehicleId { get; set; }

	// ── Odometer (kilométrage) ────────────────────────────────────────────────

	/// <summary>Kilomètres parcourus par ce véhicule. Sauvegardé par VehicleId.</summary>
	[Sync( SyncFlags.FromHost )]
	public float Odometer { get; private set; }

	/// <summary>Kilomètres à partir desquels la puissance commence à baisser.</summary>
	[Property, Group( "Odometer" )] public float WearStartKm { get; set; } = 500f;

	/// <summary>Kilomètres où la puissance est au minimum.</summary>
	[Property, Group( "Odometer" )] public float WearMaxKm { get; set; } = 5000f;

	/// <summary>Puissance minimum en % quand l'usure est maximale (0.5 = 50%).</summary>
	[Property, Group( "Odometer" )] public float WearMinPower { get; set; } = 0.5f;

	/// <summary>Multiplicateur de puissance actuel basé sur l'usure (1.0 = neuf).</summary>
	public float WearPowerFactor { get; private set; } = 1.0f;

	/// <summary>Intervalle de sauvegarde du kilométrage (en km).</summary>
	[Property, Group( "Odometer" )] public float OdometerSaveInterval { get; set; } = 1f;

	private Vector3 _lastOdometerPos;
	private float _odometerUnsaved;
	private bool _odometerInitialized;

	// ── Fuel Persistence ──────────────────────────────────────────────────────

	/// <summary>Intervalle de sauvegarde du carburant (en litres consommés).</summary>
	[Property, Group( "Fuel" )] public float FuelSaveInterval { get; set; } = 0.5f;

	private float _fuelUnsaved;
	private float _fuelLastLevel;

	// ── Inventory ─────────────────────────────────────────────────────────────

	/// <summary>Nombre de slots de la boîte à gants.</summary>
	[Property, Group( "Inventory" )] public int GloveBoxCapacity { get; set; } = 6;

	/// <summary>Poids max de la boîte à gants (kg).</summary>
	[Property, Group( "Inventory" )] public float GloveBoxMaxWeight { get; set; } = 10f;

	/// <summary>Nombre de slots du coffre.</summary>
	[Property, Group( "Inventory" )] public int TrunkCapacity { get; set; } = 24;

	/// <summary>Poids max du coffre (kg).</summary>
	[Property, Group( "Inventory" )] public float TrunkMaxWeight { get; set; } = 50f;

	/// <summary>Active les logs de debug pour l'inventaire véhicule (touche I, ouverture, recherche storage).</summary>
	[Property, Group( "Inventory" )] public bool ShowInventoryLogs { get; set; } = false;

	// ── Debug ─────────────────────────────────────────────────────────────────

	/// <summary>Shows per-wheel force arrows and text overlays at runtime.</summary>
	[Property] [Group( "Debug" )] public bool ShowDebugGizmos { get; set; } = false;

	/// <summary>Shows the debug HUD panel with drivetrain, wheel, and rotation info.</summary>
	[Property] [Group( "Debug" )] public bool ShowDebugHUD { get; set; } = false;

	/// <summary>Prints detailed vehicle logs (seat enter, wheel info, IK debug, etc.).</summary>
	[Property] [Group( "Debug" )] public bool ShowDebugLogs { get; set; } = false;

	// ── Suspension Log ────────────────────────────────────────────────────────

	[Button( "Log Suspension (5s)" ), Group( "Debug" )]
	public void StartSuspensionLog()
	{
		_suspLogActive = true;
		_suspLogTime = 0f;
		_suspLogTimer = 0f;
		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( $"[SuspLog] STARTED — Logging suspension for 5s" );
		Log.Info( $"[SuspLog] Mass={Mass}kg Sleeping={Rigidbody?.PhysicsBody?.Sleeping}" );
		foreach ( var w in AllWheels() )
			Log.Info( $"[SuspLog]   {w.GameObject.Name}: LastLen={w.LastLength:F2} RestLen={w.RestLength} Comp={w.Compression:F3} Fz={w.Fz:F0} Grounded={w.IsGrounded}" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	private bool _suspLogActive;
	private float _suspLogTime;
	private float _suspLogTimer;

	private void UpdateSuspensionLog()
	{
		if ( !_suspLogActive || !Rigidbody.IsValid() )
			return;

		_suspLogTime += Time.Delta;
		_suspLogTimer += Time.Delta;

		if ( _suspLogTimer >= 0.1f )
		{
			_suspLogTimer = 0f;
			var rb = Rigidbody;
			bool sleeping = rb.PhysicsBody?.Sleeping ?? false;
			float vel = rb.Velocity.Length;
			float vertVel = rb.Velocity.z;
			string wheelStr = string.Join( " | ", AllWheels().Select( w =>
				$"{w.GameObject.Name}: LL={w.LastLength:F1} C={w.Compression:F3} Fz={w.Fz:F0} G={w.IsGrounded}" ) );
			Log.Info( $"[SuspLog] t={_suspLogTime:F2}s | sleep={sleeping} | vel={vel:F1} vZ={vertVel:F1} | {wheelStr}" );
		}

		if ( _suspLogTime >= 5f )
		{
			_suspLogActive = false;
			Log.Info( "[SuspLog] DONE" );
		}
	}

	// ── Acceleration Test ─────────────────────────────────────────────────────

	/// <summary>Runs an automatic full-throttle acceleration test with detailed logging.</summary>
	[Button( "Run Accel Test" ), Group( "Debug" )]
	public void StartAccelTest()
	{
		_accelTestActive = true;
		_accelTestTime = 0f;
		_accelTestLastGear = -999;
		_accelTestLogTimer = 0f;
		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( "[AccelTest] STARTED — Full throttle, logging every 0.25s" );
		Log.Info( $"[AccelTest] Mass={Mass}kg MaxRPM={Engine?.MaxRPM} HP={Engine?.HorsePower} Gears={Gearbox?.NumForwardGears}" );
		Log.Info( $"[AccelTest] ShiftUp={Gearbox?.ShiftUpRpm} ShiftDown={Gearbox?.ShiftDownRpm} ShiftTime={Gearbox?.ShiftTime}s ShiftDelay={Gearbox?.ShiftDelay}s" );
		Log.Info( $"[AccelTest] GearRatios=[{string.Join( ", ", Gearbox?.ForwardGearRatios ?? new() )}] DiffRatio={Engine?.DifferentialRatio}" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	[Button( "Stop Accel Test" ), Group( "Debug" )]
	public void StopAccelTest()
	{
		if ( !_accelTestActive ) return;
		_accelTestActive = false;
		Log.Info( "[AccelTest] STOPPED" );
	}

	private bool _accelTestActive;
	private float _accelTestTime;
	private int _accelTestLastGear;
	private float _accelTestLogTimer;

	private void UpdateAccelTest()
	{
		if ( !_accelTestActive || !Engine.IsValid() || !Gearbox.IsValid() )
			return;

		// Force full throttle
		InputState.direction = new Vector3( 1f, 0f, 0f );

		_accelTestTime += Time.Delta;
		_accelTestLogTimer += Time.Delta;

		// Detect gear change
		if ( Gearbox.CurrentGear != _accelTestLastGear )
		{
			string shiftDir = Gearbox.CurrentGear > _accelTestLastGear ? "UP" : "DOWN";
			if ( _accelTestLastGear != -999 )
				Log.Info( $"[AccelTest] *** SHIFT {shiftDir} *** {_accelTestLastGear} → {Gearbox.CurrentGear} at t={_accelTestTime:F2}s" );
			_accelTestLastGear = Gearbox.CurrentGear;
		}

		// Log every 0.25s
		if ( _accelTestLogTimer >= 0.25f )
		{
			_accelTestLogTimer = 0f;

			float speedMs = GetSpeed();
			float speedKmh = speedMs * 3.6f;
			float rpm = Engine.RPM;
			int gear = Gearbox.CurrentGear;
			float ratio = Gearbox.Ratio;
			float diffRatio = Engine.DifferentialRatio;
			float totalRatio = ratio * diffRatio;
			float engineTorque = Engine.GetEffectiveTorque();
			float clutchTorque = DbgClutchTorque;
			float wheelTorque = engineTorque * totalRatio;
			bool shifting = Gearbox.IsShifting;
			float tcFactor = DbgTractionControl;

			// Wheel info
			float avgSlip = 0f;
			float avgFz = 0f;
			int wCount = 0;
			foreach ( var w in AllWheels() )
			{
				if ( !w.IsValid() ) continue;
				avgSlip += MathF.Abs( w.SlipRatio );
				avgFz += w.Fz;
				wCount++;
			}
			if ( wCount > 0 ) { avgSlip /= wCount; avgFz /= wCount; }

			var rbVel = Rigidbody.IsValid() ? Rigidbody.Velocity.Length * 0.0254f : 0f; // to m/s

			Log.Info( $"[AccelTest] t={_accelTestTime:F2}s | {speedKmh:F1}km/h ({speedMs:F2}m/s) | G{gear}{(shifting ? " SHIFTING" : "")} ratio={ratio:F3}(x{diffRatio:F2}={totalRatio:F2}) | RPM={rpm:F0} | EngTq={engineTorque:F1} WhlTq={wheelTorque:F1} | TC={tcFactor:F2} Slip={avgSlip:F3} Fz={avgFz:F0}" );

			// Stop at 200 km/h or 30s
			if ( speedKmh > 200f || _accelTestTime > 30f )
			{
				Log.Info( $"[AccelTest] FINISHED — {speedKmh:F1}km/h in {_accelTestTime:F2}s" );
				_accelTestActive = false;
			}
		}
	}

	// ── Brake Test ────────────────────────────────────────────────────────────

	/// <summary>Target speed in km/h to reach before braking.</summary>
	[Property, Group( "Debug" )] public float BrakeTestTargetKmh { get; set; } = 100f;

	/// <summary>Runs a brake test: accelerates to target speed, then full brake until stopped.</summary>
	[Button( "Run Brake Test" ), Group( "Debug" )]
	public void StartBrakeTest()
	{
		_brakeTestActive = true;
		_brakeTestPhase = BrakeTestPhase.Accelerating;
		_brakeTestTime = 0f;
		_brakeTestBrakeStartTime = 0f;
		_brakeTestBrakeStartSpeed = 0f;
		_brakeTestLogTimer = 0f;
		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( $"[BrakeTest] STARTED — Accelerating to {BrakeTestTargetKmh:F0}km/h then full brake" );
		Log.Info( $"[BrakeTest] MaxBrakeTorque={MaxBrakeTorque} BrakeBias={BrakeBias:F2} ABS={ABS:F1} Mass={Mass}kg" );
		Log.Info( $"[BrakeTest] ThrottleSpeed={ThrottleSpeed} (brake smoothing = ThrottleSpeed*2.0 = {ThrottleSpeed * 2.0f:F1})" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	[Button( "Stop Brake Test" ), Group( "Debug" )]
	public void StopBrakeTest()
	{
		if ( !_brakeTestActive ) return;
		_brakeTestActive = false;
		Log.Info( "[BrakeTest] STOPPED" );
	}

	private enum BrakeTestPhase { Accelerating, Braking }
	private bool _brakeTestActive;
	private BrakeTestPhase _brakeTestPhase;
	private float _brakeTestTime;
	private float _brakeTestBrakeStartTime;
	private float _brakeTestBrakeStartSpeed;
	private float _brakeTestLogTimer;

	private void UpdateBrakeTest()
	{
		if ( !_brakeTestActive || !Engine.IsValid() )
			return;

		_brakeTestTime += Time.Delta;
		_brakeTestLogTimer += Time.Delta;

		float speedMs = GetSpeed();
		float speedKmh = speedMs * 3.6f;

		if ( _brakeTestPhase == BrakeTestPhase.Accelerating )
		{
			// Full throttle until target speed
			InputState.direction = new Vector3( 1f, 0f, 0f );

			if ( speedKmh >= BrakeTestTargetKmh )
			{
				_brakeTestPhase = BrakeTestPhase.Braking;
				_brakeTestBrakeStartTime = _brakeTestTime;
				_brakeTestBrakeStartSpeed = speedKmh;
				Log.Info( $"[BrakeTest] ═══ TARGET REACHED: {speedKmh:F1}km/h at t={_brakeTestTime:F2}s — FULL BRAKE ═══" );
			}
			else if ( _brakeTestLogTimer >= 1f )
			{
				_brakeTestLogTimer = 0f;
				Log.Info( $"[BrakeTest] Accelerating... {speedKmh:F1}km/h" );
			}
			return;
		}

		// Braking phase: full reverse input to trigger braking
		InputState.direction = new Vector3( -1f, 0f, 0f );

		if ( _brakeTestLogTimer >= 0.25f )
		{
			_brakeTestLogTimer = 0f;

			float brakeTime = _brakeTestTime - _brakeTestBrakeStartTime;
			float rpm = Engine.RPM;
			int gear = Gearbox.IsValid() ? Gearbox.CurrentGear : 0;
			float absMod = DbgABS;

			// Per-wheel details
			string wheelInfo = "";
			foreach ( var w in AllWheels() )
			{
				if ( !w.IsValid() ) continue;
				wheelInfo += $" [{w.GameObject.Name}: SR={w.SlipRatio:F3} Fz={w.Fz:F0} Brk={w.BrakeTorque:F0}]";
			}

			float decel = brakeTime > 0.01f ? (_brakeTestBrakeStartSpeed - speedKmh) / brakeTime : 0f;

			Log.Info( $"[BrakeTest] t={brakeTime:F2}s | {speedKmh:F1}km/h | Decel={decel:F1}km/h/s | BrakeIn={_smoothBrake:F3} | ABS={absMod:F2} | G{gear} RPM={rpm:F0}{wheelInfo}" );

			// Stopped
			if ( speedKmh < 1f )
			{
				float totalDist = (_brakeTestBrakeStartSpeed / 3.6f) * brakeTime * 0.5f; // rough estimate
				Log.Info( $"[BrakeTest] ═══ STOPPED ═══" );
				Log.Info( $"[BrakeTest] {_brakeTestBrakeStartSpeed:F1}km/h → 0 in {brakeTime:F2}s" );
				Log.Info( $"[BrakeTest] Avg decel: {_brakeTestBrakeStartSpeed / brakeTime:F1}km/h/s ({_brakeTestBrakeStartSpeed / 3.6f / brakeTime:F2}m/s²)" );
				Log.Info( $"[BrakeTest] Est. distance: ~{totalDist:F1}m" );
				_brakeTestActive = false;
			}

			if ( brakeTime > 20f )
			{
				Log.Info( $"[BrakeTest] TIMEOUT — still at {speedKmh:F1}km/h after 20s" );
				_brakeTestActive = false;
			}
		}
	}

	// ── Cornering Test ────────────────────────────────────────────────────────

	/// <summary>Target speed in km/h to reach before turning.</summary>
	[Property, Group( "Debug" )] public float CornerTestTargetKmh { get; set; } = 80f;

	/// <summary>Throttle during turn phase (0-1). 1 = full gas, 0 = lift off.</summary>
	[Property, Group( "Debug" )] public float CornerTestThrottle { get; set; } = 0.5f;

	/// <summary>Runs a cornering test: accelerate to speed, then full left turn.</summary>
	[Button( "Run Corner Test" ), Group( "Debug" )]
	public void StartCornerTest()
	{
		_cornerTestActive = true;
		_cornerTestPhase = CornerTestPhase.Accelerating;
		_cornerTestTime = 0f;
		_cornerTestTurnTime = 0f;
		_cornerTestLogTimer = 0f;
		var steering = Components.GetInAncestorsOrSelf<Steering>();
		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( $"[CornerTest] STARTED — Accel to {CornerTestTargetKmh:F0}km/h then full left turn at {CornerTestThrottle:F0}% throttle" );
		Log.Info( $"[CornerTest] Mass={Mass}kg Drivetrain={Drivetrain} MaxSteer={steering?.MaxSteerAngle:F1}°" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	[Button( "Stop Corner Test" ), Group( "Debug" )]
	public void StopCornerTest()
	{
		if ( !_cornerTestActive ) return;
		_cornerTestActive = false;
		Log.Info( "[CornerTest] STOPPED" );
	}

	private enum CornerTestPhase { Accelerating, Turning }
	private bool _cornerTestActive;
	private CornerTestPhase _cornerTestPhase;
	private float _cornerTestTime;
	private float _cornerTestTurnTime;
	private float _cornerTestLogTimer;

	private void UpdateCornerTest()
	{
		if ( !_cornerTestActive )
			return;

		_cornerTestTime += Time.Delta;
		_cornerTestLogTimer += Time.Delta;

		float speedMs = GetSpeed();
		float speedKmh = speedMs * 3.6f;

		if ( _cornerTestPhase == CornerTestPhase.Accelerating )
		{
			InputState.direction = new Vector3( 1f, 0f, 0f );

			if ( speedKmh >= CornerTestTargetKmh )
			{
				_cornerTestPhase = CornerTestPhase.Turning;
				_cornerTestTurnTime = 0f;
				Log.Info( $"[CornerTest] ═══ TARGET REACHED: {speedKmh:F1}km/h — FULL LEFT TURN ═══" );
			}
			else if ( _cornerTestLogTimer >= 1f )
			{
				_cornerTestLogTimer = 0f;
				Log.Info( $"[CornerTest] Accelerating... {speedKmh:F1}km/h" );
			}
			return;
		}

		// Turn phase: full left steer + configured throttle
		InputState.direction = new Vector3( CornerTestThrottle, -1f, 0f );
		_cornerTestTurnTime += Time.Delta;

		if ( _cornerTestLogTimer >= 0.25f )
		{
			_cornerTestLogTimer = 0f;

			var steering = Components.GetInAncestorsOrSelf<Steering>();
			float steerAngle = steering?.CurrentAngle ?? 0f;
			int gear = Gearbox.IsValid() ? Gearbox.CurrentGear : 0;
			float rpm = Engine.IsValid() ? Engine.RPM : 0f;

			// Compute front and rear average slip angles
			float frontSA = 0f, rearSA = 0f;
			float frontSR = 0f, rearSR = 0f;
			float frontFy = 0f, rearFy = 0f;
			float frontFz = 0f, rearFz = 0f;
			int fc = 0, rc = 0;

			if ( Axles != null && Axles.Count >= 2 )
			{
				foreach ( var w in new[] { Axles[0]?.Left, Axles[0]?.Right } )
				{
					if ( !w.IsValid() ) continue;
					frontSA += MathF.Abs( w.DynamicSlipAngle );
					frontSR += MathF.Abs( w.SlipRatio );
					frontFy += MathF.Abs( w.Fy );
					frontFz += w.Fz;
					fc++;
				}
				foreach ( var w in new[] { Axles[1]?.Left, Axles[1]?.Right } )
				{
					if ( !w.IsValid() ) continue;
					rearSA += MathF.Abs( w.DynamicSlipAngle );
					rearSR += MathF.Abs( w.SlipRatio );
					rearFy += MathF.Abs( w.Fy );
					rearFz += w.Fz;
					rc++;
				}
			}
			if ( fc > 0 ) { frontSA /= fc; frontSR /= fc; frontFy /= fc; frontFz /= fc; }
			if ( rc > 0 ) { rearSA /= rc; rearSR /= rc; rearFy /= rc; rearFz /= rc; }

			// Determine behavior
			string behavior;
			float saDiff = frontSA - rearSA;
			if ( MathF.Abs( saDiff ) < 1f )
				behavior = "NEUTRAL";
			else if ( saDiff > 0f )
				behavior = "UNDERSTEER";
			else
				behavior = "OVERSTEER";

			// Yaw rate from rigidbody
			float yawRate = Rigidbody.IsValid() ? MathF.Abs( Rigidbody.AngularVelocity.z ) : 0f;

			Log.Info( $"[CornerTest] t={_cornerTestTurnTime:F2}s | {speedKmh:F1}km/h G{gear} | Steer={steerAngle:F1}° | Front SA={frontSA:F1}° SR={frontSR:F3} Fy={frontFy:F0} Fz={frontFz:F0} | Rear SA={rearSA:F1}° SR={rearSR:F3} Fy={rearFy:F0} Fz={rearFz:F0} | YawRate={yawRate:F2} | {behavior} (diff={saDiff:F1}°)" );

			// Stop after 8 seconds of turning or if car spins out (yaw rate very high)
			if ( _cornerTestTurnTime > 8f )
			{
				Log.Info( $"[CornerTest] FINISHED — 8s of turning completed" );
				_cornerTestActive = false;
			}
			else if ( speedKmh < 5f && _cornerTestTurnTime > 2f )
			{
				Log.Info( $"[CornerTest] FINISHED — car stopped" );
				_cornerTestActive = false;
			}
		}
	}

	// ── Suspension Drop Test ──────────────────────────────────────────────────

	[Property, Group( "Debug" )] public float SuspDropHeight1 { get; set; } = 20f;
	[Property, Group( "Debug" )] public float SuspDropHeight2 { get; set; } = 50f;
	[Property, Group( "Debug" )] public float SuspDropHeight3 { get; set; } = 100f;

	[Button( "Run Suspension Test" ), Group( "Debug" )]
	public void StartSuspensionTest()
	{
		_suspTestActive = true;
		_suspTestPhase = SuspTestPhase.IdleMonitor;
		_suspTestDropIndex = 0;
		_suspTestTime = 0f;
		_suspTestLogTimer = 0f;
		_suspTestSettleTimer = 0f;
		_suspTestIdleStableTime = 0f;
		_suspTestMaxCompression = new float[4];
		_suspTestMaxFz = new float[4];
		_suspTestBounceCount = 0;
		_suspTestLastWasRising = false;
		_suspTestGroundPos = WorldPosition;

		// Wake up the physics body if sleeping
		if ( Rigidbody.IsValid() && Rigidbody.PhysicsBody is not null )
			Rigidbody.PhysicsBody.Sleeping = false;

		var wheels = AllWheels().ToArray();
		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( $"[SuspTest] STARTED — Drop test at {SuspDropHeight1}/{SuspDropHeight2}/{SuspDropHeight3} inches" );
		Log.Info( $"[SuspTest] Mass={Mass}kg" );
		foreach ( var w in wheels )
			Log.Info( $"[SuspTest]   {w.GameObject.Name}: Spring={w.SpringStiffness} Damper={w.DamperStiffness} RestLen={w.RestLength} Radius={w.Radius}" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	[Button( "Stop Suspension Test" ), Group( "Debug" )]
	public void StopSuspensionTest()
	{
		if ( !_suspTestActive ) return;
		_suspTestActive = false;
		Log.Info( "[SuspTest] STOPPED" );
	}

	private enum SuspTestPhase { IdleMonitor, Lifting, Stabilizing, Dropping, Measuring, NextDrop, Resettling }
	private bool _suspTestActive;
	private SuspTestPhase _suspTestPhase;
	private int _suspTestDropIndex;
	private float _suspTestTime;
	private float _suspTestLogTimer;
	private float _suspTestSettleTimer;
	private float[] _suspTestMaxCompression = new float[4];
	private float[] _suspTestMaxFz = new float[4];
	private int _suspTestBounceCount;
	private bool _suspTestLastWasRising;
	private float _suspTestIdleStableTime;
	private Vector3 _suspTestGroundPos;

	private float GetDropHeight( int index )
	{
		return index switch
		{
			0 => SuspDropHeight1,
			1 => SuspDropHeight2,
			2 => SuspDropHeight3,
			_ => 0f
		};
	}

	private void UpdateSuspensionTest()
	{
		if ( !_suspTestActive || !Rigidbody.IsValid() )
			return;

		_suspTestTime += Time.Delta;
		_suspTestLogTimer += Time.Delta;

		var rb = Rigidbody;
		var wheels = AllWheels().ToArray();
		float dropHeight = GetDropHeight( _suspTestDropIndex );

		switch ( _suspTestPhase )
		{
			case SuspTestPhase.IdleMonitor:
			{
				_suspTestSettleTimer += Time.Delta;
				float absVel = rb.Velocity.Length;
				bool allGrounded = wheels.All( w => w.IsGrounded );

				// Track how long the car has been stable (3 in/s threshold matches park sleep)
				if ( absVel < 3f && allGrounded )
					_suspTestIdleStableTime += Time.Delta;
				else
					_suspTestIdleStableTime = 0f;

				if ( _suspTestLogTimer >= 0.5f )
				{
					_suspTestLogTimer = 0f;
					float vertVel = rb.Velocity.z;
					float horizVel = rb.Velocity.WithZ( 0f ).Length;
					string compStr = string.Join( " ", wheels.Take( 4 ).Select( w => $"{w.GameObject.Name}:C={w.Compression:F3}/Fz={w.Fz:F0}" ) );
					Log.Info( $"[SuspTest] IDLE t={_suspTestSettleTimer:F2}s stable={_suspTestIdleStableTime:F1}s | vel=({horizVel:F1},{vertVel:F1}) | grounded={allGrounded} | {compStr}" );
				}

				// Need 5 consecutive seconds of stability before starting drops
				if ( _suspTestIdleStableTime >= 5f )
				{
					_suspTestGroundPos = WorldPosition;
					Log.Info( $"[SuspTest] ─── IDLE PHASE COMPLETE ───" );
					float avgComp = wheels.Take( 4 ).Average( w => w.Compression );
					float avgFz = wheels.Take( 4 ).Average( w => w.Fz );
					Log.Info( $"[SuspTest] Avg rest compression: {avgComp:F3} ({avgComp * 100:F1}%) | Avg Fz: {avgFz:F0}" );
					Log.Info( $"[SuspTest] Ground pos: {_suspTestGroundPos}" );
					_suspTestPhase = SuspTestPhase.Lifting;
					_suspTestSettleTimer = 0f;
					_suspTestLogTimer = 0f;
				}

				// Timeout after 30s
				if ( _suspTestSettleTimer > 30f )
				{
					_suspTestGroundPos = WorldPosition;
					Log.Info( $"[SuspTest] IDLE TIMEOUT — starting drops anyway" );
					_suspTestPhase = SuspTestPhase.Lifting;
					_suspTestSettleTimer = 0f;
					_suspTestLogTimer = 0f;
				}
				break;
			}

			case SuspTestPhase.Lifting:
			{
				// Teleport car up and freeze it
				Vector3 targetPos = _suspTestGroundPos + Vector3.Up * dropHeight;
				WorldPosition = targetPos;
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				_suspTestSettleTimer = 0f;

				Log.Info( $"[SuspTest] ═══ DROP #{_suspTestDropIndex + 1}: {dropHeight} inches ═══" );
				_suspTestPhase = SuspTestPhase.Stabilizing;
				break;
			}

			case SuspTestPhase.Stabilizing:
			{
				// Hold car still for 0.5s to let suspension settle in air
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				_suspTestSettleTimer += Time.Delta;

				if ( _suspTestSettleTimer >= 0.5f )
				{
					// Release!
					_suspTestPhase = SuspTestPhase.Dropping;
					_suspTestSettleTimer = 0f;
					_suspTestBounceCount = 0;
					_suspTestLastWasRising = false;
					for ( int i = 0; i < 4 && i < wheels.Length; i++ )
					{
						_suspTestMaxCompression[i] = 0f;
						_suspTestMaxFz[i] = 0f;
					}
					Log.Info( $"[SuspTest] RELEASED at height={dropHeight} inches" );
				}
				break;
			}

			case SuspTestPhase.Dropping:
			case SuspTestPhase.Measuring:
			{
				_suspTestSettleTimer += Time.Delta;
				float vertVel = rb.Velocity.z; // positive = up in s&box

				// Detect bounce: velocity goes from negative (falling) to positive (rising)
				if ( _suspTestPhase == SuspTestPhase.Measuring || AllWheels().Any( w => w.IsGrounded ) )
				{
					if ( _suspTestPhase == SuspTestPhase.Dropping )
					{
						_suspTestPhase = SuspTestPhase.Measuring;
						_suspTestSettleTimer = 0f;
						Log.Info( $"[SuspTest] CONTACT! vel={rb.Velocity.z:F1} in/s" );
					}

					// Track bounces
					bool isRising = vertVel > 1f;
					if ( isRising && !_suspTestLastWasRising && _suspTestSettleTimer > 0.05f )
						_suspTestBounceCount++;
					_suspTestLastWasRising = isRising;

					// Track max compression and Fz per wheel
					for ( int i = 0; i < wheels.Length && i < 4; i++ )
					{
						if ( wheels[i].Compression > _suspTestMaxCompression[i] )
							_suspTestMaxCompression[i] = wheels[i].Compression;
						if ( wheels[i].Fz > _suspTestMaxFz[i] )
							_suspTestMaxFz[i] = wheels[i].Fz;
					}
				}

				// Log every 0.1s during drop/measure
				if ( _suspTestLogTimer >= 0.1f )
				{
					_suspTestLogTimer = 0f;
					bool anyGrounded = wheels.Any( w => w.IsGrounded );
					string compStr = string.Join( " ", wheels.Take( 4 ).Select( ( w, i ) => $"{w.GameObject.Name}:{w.Compression:F2}/{w.Fz:F0}" ) );
					float heightAboveGround = (WorldPosition.z - _suspTestGroundPos.z);
					Log.Info( $"[SuspTest] t={_suspTestSettleTimer:F2}s | h={heightAboveGround:F1}in | vel={vertVel:F1} | grounded={anyGrounded} | bounces={_suspTestBounceCount} | {compStr}" );
				}

				// Check if settled: all wheels grounded, low velocity, enough time passed
				if ( _suspTestPhase == SuspTestPhase.Measuring )
				{
					float absTotal = rb.Velocity.Length;
					bool allSettled = wheels.All( w => w.IsGrounded );
					if ( absTotal < 3f && allSettled && _suspTestSettleTimer > 2.0f )
					{
						// Settled — print summary
						Log.Info( $"[SuspTest] ─── DROP #{_suspTestDropIndex + 1} RESULTS ({dropHeight} inches) ───" );
						Log.Info( $"[SuspTest] Bounces: {_suspTestBounceCount} | Settle time: {_suspTestSettleTimer:F2}s" );
						for ( int i = 0; i < wheels.Length && i < 4; i++ )
							Log.Info( $"[SuspTest]   {wheels[i].GameObject.Name}: MaxCompression={_suspTestMaxCompression[i]:F2} ({_suspTestMaxCompression[i] * 100:F0}%) MaxFz={_suspTestMaxFz[i]:F0} FinalCompression={wheels[i].Compression:F2}" );

						float maxComp = _suspTestMaxCompression.Take( wheels.Length ).Max();
						if ( maxComp >= 1.0f )
							Log.Info( $"[SuspTest] ⚠ BOTTOMED OUT — suspension fully compressed!" );
						else if ( maxComp >= 0.8f )
							Log.Info( $"[SuspTest] ⚠ Near bottom-out ({maxComp * 100:F0}% max compression)" );
						else
							Log.Info( $"[SuspTest] ✓ Suspension OK (max {maxComp * 100:F0}% compression)" );

						if ( _suspTestBounceCount > 3 )
							Log.Info( $"[SuspTest] ⚠ Too many bounces ({_suspTestBounceCount}) — increase DamperStiffness" );
						else if ( _suspTestBounceCount <= 1 )
							Log.Info( $"[SuspTest] ✓ Well damped ({_suspTestBounceCount} bounces)" );

						_suspTestPhase = SuspTestPhase.NextDrop;
						_suspTestSettleTimer = 0f;
					}

					// Timeout after 8s
					if ( _suspTestSettleTimer > 8f )
					{
						Log.Info( $"[SuspTest] TIMEOUT — drop #{_suspTestDropIndex + 1} didn't settle" );
						_suspTestPhase = SuspTestPhase.NextDrop;
						_suspTestSettleTimer = 0f;
					}
				}

				// Timeout in dropping phase (never hit ground)
				if ( _suspTestPhase == SuspTestPhase.Dropping && _suspTestSettleTimer > 5f )
				{
					Log.Info( $"[SuspTest] TIMEOUT — never hit ground" );
					_suspTestPhase = SuspTestPhase.NextDrop;
					_suspTestSettleTimer = 0f;
				}
				break;
			}

			case SuspTestPhase.NextDrop:
			{
				// Return car to ground position and start resettling
				WorldPosition = _suspTestGroundPos;
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				_suspTestSettleTimer = 0f;
				_suspTestPhase = SuspTestPhase.Resettling;
				Log.Info( $"[SuspTest] Resettling before next drop..." );
				break;
			}

			case SuspTestPhase.Resettling:
			{
				_suspTestSettleTimer += Time.Delta;
				float absVel = rb.Velocity.Length;
				bool allGrounded = wheels.All( w => w.IsGrounded );

				// Wait until completely still: all grounded, low velocity, at least 3s
				if ( absVel < 3f && allGrounded && _suspTestSettleTimer > 3f )
				{
					_suspTestDropIndex++;
					if ( _suspTestDropIndex >= 3 )
					{
						Log.Info( $"[SuspTest] ══════════════════════════════════════════════════" );
						Log.Info( $"[SuspTest] ALL DROPS COMPLETE" );
						Log.Info( $"[SuspTest] ══════════════════════════════════════════════════" );
						_suspTestActive = false;
					}
					else
					{
						_suspTestGroundPos = WorldPosition;
						Log.Info( $"[SuspTest] Settled. Starting next drop." );
						_suspTestPhase = SuspTestPhase.Lifting;
						_suspTestSettleTimer = 0f;
					}
				}

				// Timeout
				if ( _suspTestSettleTimer > 5f )
				{
					_suspTestDropIndex++;
					if ( _suspTestDropIndex >= 3 )
					{
						_suspTestActive = false;
						Log.Info( $"[SuspTest] ALL DROPS COMPLETE (settle timeout)" );
					}
					else
					{
						_suspTestGroundPos = WorldPosition;
						_suspTestPhase = SuspTestPhase.Lifting;
						_suspTestSettleTimer = 0f;
					}
				}
				break;
			}
		}
	}

	public VehicleInputState InputState { get; } = new();

	// ── Synced debug values (read by HUD -- synced so passengers see them too) ─
	[Sync( SyncFlags.FromHost )] public float DbgThrottle         { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgBrakeInput       { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgCarSpeed         { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgEngineTorque     { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool  DbgEngineValid      { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool  DbgGearboxValid     { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgTractionControl  { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgABS              { get; private set; }
	[Sync( SyncFlags.FromHost )] public float DbgClutchTorque     { get; private set; }
	[Sync( SyncFlags.FromHost )] public bool  IsHandbrakeActive   { get; private set; }

	// ── Private state ─────────────────────────────────────────────────────────
	private GameObject _fire;
	private bool _loggedDriverEnter;
	private float _smoothThrottle;
	private float _smoothBrake;
	private float _smoothHandbrake;
	private float _tractionControlFactor = 1.0f;
	private Vector3 _baseInertia;
	private bool _wasBackwardReleased = true;
	private bool _wasForwardReleased = true;

	// ABS modulation state per axle
	private float _absFrontMod = 1.0f;
	private float _absRearMod = 1.0f;
	private const float AbsModulationRate = 15.0f;

	// Gear shift tracking
	private bool _wasShifting;
	private int _lastGear;

	// ── OnStart ───────────────────────────────────────────────────────────────

	protected override void OnStart()
	{
		// Assign a unique ID if this vehicle doesn't have one yet
		if ( Networking.IsHost && VehicleId == Guid.Empty )
			VehicleId = Guid.NewGuid();

		// Charger le kilométrage et le carburant sauvegardés
		LoadOdometer();
		LoadFuel();
		if ( Engine.IsValid() )
			_fuelLastLevel = Engine.FuelLevel;

		if ( !Rigidbody.IsValid() || Rigidbody.PhysicsBody is null )
			return;

		// Disable built-in gravity -- we apply our own custom gravity + downforce
		Rigidbody.Gravity = false;

		// Override mass and center of mass
		Rigidbody.OverrideMassCenter = true;
		Rigidbody.MassCenterOverride = CenterOfMass;
		Rigidbody.MassOverride = TotalMass;

		// Capture base inertia before we scale it
		_baseInertia = Rigidbody.PhysicsBody.Inertia;

		// Apply inertia tensor scale
		if ( InertiaTensorScale != Vector3.One )
		{
			Rigidbody.PhysicsBody.SetInertiaTensor(
				_baseInertia * InertiaTensorScale,
				Rotation.Identity
			);
		}
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	// ── Odometer Methods ──────────────────────────────────────────────────────

	private static string OdometerPath( Guid id ) => $"vehicles/{id}/odometer.json";

	private void LoadOdometer()
	{
		if ( !Networking.IsHost || VehicleId == Guid.Empty ) return;

		var data = FileSystem.Data.ReadJson<OdometerData>( OdometerPath( VehicleId ) );
		if ( data != null )
			Odometer = data.Km;
	}

	private void SaveOdometer()
	{
		if ( !Networking.IsHost || VehicleId == Guid.Empty ) return;

		FileSystem.Data.WriteJson( OdometerPath( VehicleId ), new OdometerData { Km = Odometer } );
	}

	private void UpdateOdometer()
	{
		if ( !Networking.IsHost || !Rigidbody.IsValid() ) return;

		var pos = WorldPosition;

		if ( !_odometerInitialized )
		{
			_lastOdometerPos = pos;
			_odometerInitialized = true;
			return;
		}

		// Distance en inches → convertir en km (1 inch = 0.0000254 km)
		float distInches = _lastOdometerPos.Distance( pos );
		_lastOdometerPos = pos;

		// Ignorer les téléportations (>50m par frame)
		if ( distInches > 2000f ) return;

		float distKm = distInches * 0.0000254f;
		Odometer += distKm;
		_odometerUnsaved += distKm;

		// Usure des composants internes
		if ( Internals.IsValid() )
			Internals.AddDistance( distKm );

		// Sauvegarder périodiquement
		if ( _odometerUnsaved >= OdometerSaveInterval )
		{
			_odometerUnsaved = 0f;
			SaveOdometer();
		}

		// Calculer le facteur d'usure
		if ( Odometer <= WearStartKm )
		{
			WearPowerFactor = 1.0f;
		}
		else if ( Odometer >= WearMaxKm )
		{
			WearPowerFactor = WearMinPower;
		}
		else
		{
			float t = (Odometer - WearStartKm) / (WearMaxKm - WearStartKm);
			WearPowerFactor = 1.0f - t * (1.0f - WearMinPower);
		}

		// Appliquer au moteur (usure km + composants internes)
		if ( Engine.IsValid() )
		{
			float internalsFactor = Internals.IsValid() ? Internals.EnginePowerFactor : 1f;
			Engine.PowerMultiplier = WearPowerFactor * internalsFactor;
		}
	}

	// ── Fuel Methods ─────────────────────────────────────────────────────────

	private static string FuelPath( Guid id ) => $"vehicles/{id}/fuel.json";

	private void LoadFuel()
	{
		if ( !Networking.IsHost || VehicleId == Guid.Empty || !Engine.IsValid() ) return;

		var data = FileSystem.Data.ReadJson<FuelData>( FuelPath( VehicleId ) );
		if ( data != null )
			Engine.FuelLevel = data.Litres;
	}

	private void SaveFuel()
	{
		if ( !Networking.IsHost || VehicleId == Guid.Empty || !Engine.IsValid() ) return;

		FileSystem.Data.WriteJson( FuelPath( VehicleId ), new FuelData { Litres = Engine.FuelLevel } );
	}

	private void UpdateFuel()
	{
		if ( !Networking.IsHost || !Engine.IsValid() ) return;

		float consumed = _fuelLastLevel - Engine.FuelLevel;
		if ( consumed > 0f )
			_fuelUnsaved += consumed;
		_fuelLastLevel = Engine.FuelLevel;

		if ( _fuelUnsaved >= FuelSaveInterval )
		{
			_fuelUnsaved = 0f;
			SaveFuel();
		}
	}

	// ── Fuel Test ─────────────────────────────────────────────────────────────

	private enum FuelTestPhase { Consumption, Alarm, AlarmCheck2, Sputter }

	[Button( "Run Fuel Test" ), Group( "Debug" )]
	public void StartFuelTest()
	{
		if ( !Engine.IsValid() )
		{
			Log.Warning( "[FuelTest] No engine!" );
			return;
		}
		_fuelTestActive = true;
		_fuelTestPhase = FuelTestPhase.Consumption;
		_fuelTestTime = 0f;
		_fuelTestLogTimer = 0f;
		_fuelTestSavedFuel = Engine.FuelLevel;

		Engine.FuelLevel = Engine.FuelCapacity * 0.5f;

		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( "[FuelTest] STARTED — Phase 1: Consumption Test" );
		Log.Info( $"[FuelTest] Capacity={Engine.FuelCapacity}L Rate={Engine.FuelConsumptionRate}L/h Reserve={Engine.FuelReserveThreshold * 100:F0}% Sputter={Engine.FuelSputterThreshold * 100:F0}%" );
		Log.Info( "══════════════════════════════════════════════════════════" );
	}

	[Button( "Stop Fuel Test" ), Group( "Debug" )]
	public void StopFuelTest()
	{
		if ( !_fuelTestActive ) return;
		_fuelTestActive = false;
		if ( Engine.IsValid() )
			Engine.FuelLevel = _fuelTestSavedFuel;
		Log.Info( "[FuelTest] STOPPED — Fuel restored" );
	}

	private bool _fuelTestActive;
	private FuelTestPhase _fuelTestPhase;
	private float _fuelTestTime;
	private float _fuelTestLogTimer;
	private float _fuelTestSavedFuel;

	private void UpdateFuelTest()
	{
		if ( !_fuelTestActive || !Engine.IsValid() )
			return;

		_fuelTestTime += Time.Delta;
		_fuelTestLogTimer += Time.Delta;

		switch ( _fuelTestPhase )
		{
			case FuelTestPhase.Consumption:
			{
				float throttle = 0f;
				string label = "IDLE";
				if ( _fuelTestTime >= 3f && _fuelTestTime < 6f ) { throttle = 0.5f; label = "50%"; }
				else if ( _fuelTestTime >= 6f && _fuelTestTime < 9f ) { throttle = 1f; label = "100%"; }

				InputState.direction = new Vector3( throttle, 0f, 0f );

				if ( _fuelTestLogTimer >= 0.5f )
				{
					_fuelTestLogTimer = 0f;
					Log.Info( $"[FuelTest] P1-{label} t={_fuelTestTime:F1}s RPM={Engine.RPM:F0} Fuel={Engine.FuelLevel:F3}L" );
				}

				if ( _fuelTestTime >= 9f )
				{
					Log.Info( "[FuelTest] Phase 1 COMPLETE" );
					InputState.direction = Vector3.Zero;

					_fuelTestPhase = FuelTestPhase.Alarm;
					_fuelTestTime = 0f;

					float reserveLevel = Engine.FuelCapacity * Engine.FuelReserveThreshold;
					Engine.FuelLevel = reserveLevel + 1f;
					Log.Info( $"[FuelTest] Phase 2: Alarm — set fuel={Engine.FuelLevel:F2}L (above reserve={reserveLevel:F2}L)" );
				}
				break;
			}

			case FuelTestPhase.Alarm:
			{
				InputState.direction = Vector3.Zero;

				if ( _fuelTestTime >= 0.5f )
				{
					bool passAbove = !Engine.IsLowFuel;
					Log.Info( $"[FuelTest] P2 Above: IsLowFuel={Engine.IsLowFuel} → {(passAbove ? "PASS" : "FAIL")}" );

					float reserveLevel = Engine.FuelCapacity * Engine.FuelReserveThreshold;
					Engine.FuelLevel = reserveLevel * 0.5f;
					_fuelTestPhase = FuelTestPhase.AlarmCheck2;
					_fuelTestTime = 0f;
				}
				break;
			}

			case FuelTestPhase.AlarmCheck2:
			{
				InputState.direction = Vector3.Zero;

				if ( _fuelTestTime >= 0.5f )
				{
					bool passBelow = Engine.IsLowFuel;
					Log.Info( $"[FuelTest] P2 Below: fuel={Engine.FuelLevel:F2}L IsLowFuel={Engine.IsLowFuel} → {(passBelow ? "PASS" : "FAIL")}" );
					Log.Info( "[FuelTest] Phase 2 COMPLETE" );

					_fuelTestPhase = FuelTestPhase.Sputter;
					_fuelTestTime = 0f;
					Engine.FuelLevel = 0.015f;
					Log.Info( "[FuelTest] Phase 3: Sputter — set fuel=0.015L, applying throttle" );
				}
				break;
			}

			case FuelTestPhase.Sputter:
			{
				InputState.direction = new Vector3( 0.5f, 0f, 0f );

				if ( _fuelTestLogTimer >= 0.25f )
				{
					_fuelTestLogTimer = 0f;
					Log.Info( $"[FuelTest] P3 t={_fuelTestTime:F1}s Fuel={Engine.FuelLevel:F4}L Sputtering={Engine.IsSputtering} OutOfFuel={Engine.IsOutOfFuel} RPM={Engine.RPM:F0}" );
				}

				if ( Engine.IsOutOfFuel )
				{
					Log.Info( $"[FuelTest] Phase 3 PASS — Engine stalled at t={_fuelTestTime:F1}s" );
					_fuelTestActive = false;
					Engine.FuelLevel = _fuelTestSavedFuel;
					InputState.direction = Vector3.Zero;
					Log.Info( $"[FuelTest] ALL PHASES COMPLETE — Fuel restored to {_fuelTestSavedFuel:F1}L" );
					return;
				}

				if ( _fuelTestTime > 30f )
				{
					Log.Warning( "[FuelTest] Phase 3 TIMEOUT" );
					_fuelTestActive = false;
					Engine.FuelLevel = _fuelTestSavedFuel;
					InputState.direction = Vector3.Zero;
				}
				break;
			}
		}
	}

	// ── Grip Test (per drivetrain type) ──────────────────────────────────────

	/// <summary>Duration per drivetrain type (seconds).</summary>
	[Property, Group( "Debug" )] public float GripTestDuration { get; set; } = 5f;

	/// <summary>Runs a grip test: full throttle for each drivetrain type (RWD, FWD, AWD), logs per-wheel grip data.</summary>
	[Button( "Run Grip Test" ), Group( "Debug" )]
	public void StartGripTest()
	{
		if ( !Engine.IsValid() )
		{
			Log.Warning( "[GripTest] No engine!" );
			return;
		}

		_gripTestActive = true;
		_gripTestTypeIndex = 0;
		_gripTestTime = 0f;
		_gripTestLogTimer = 0f;
		_gripTestSavedDrivetrain = Engine.Drivetrain;
		_gripTestTypes = new[] { DrivetrainType.RWD, DrivetrainType.FWD, DrivetrainType.AWD };
		_gripTestPhase = GripTestPhase.Settling;
		_gripTestSettleTime = 0f;

		Log.Info( "══════════════════════════════════════════════════════════" );
		Log.Info( $"[GripTest] STARTED — Testing RWD / FWD / AWD at full throttle for {GripTestDuration:F0}s each" );
		Log.Info( $"[GripTest] Mass={Mass}kg HP={Engine.HorsePower} DiffType={Engine.Differential.Type} TC={TractionControl:F1}" );
		Log.Info( $"[GripTest] Current drivetrain: {Engine.Drivetrain} (will be restored)" );
		foreach ( var w in AllWheels() )
			Log.Info( $"[GripTest]   {w.GameObject.Name}: FrictionMu={w.FrictionMu:F2} LoadSens={w.LoadSensitivity:F2} NomLoad={w.NominalLoad:F0}" );
		Log.Info( "══════════════════════════════════════════════════════════" );

		// Set first drivetrain type
		Engine.Drivetrain = _gripTestTypes[0];
		Log.Info( $"[GripTest] ═══ PHASE 1/3: {_gripTestTypes[0]} — Settling... ═══" );
	}

	[Button( "Stop Grip Test" ), Group( "Debug" )]
	public void StopGripTest()
	{
		if ( !_gripTestActive ) return;
		_gripTestActive = false;
		if ( Engine.IsValid() )
			Engine.Drivetrain = _gripTestSavedDrivetrain;
		InputState.direction = Vector3.Zero;
		Log.Info( $"[GripTest] STOPPED — Drivetrain restored to {_gripTestSavedDrivetrain}" );
	}

	private enum GripTestPhase { Settling, Running }
	private bool _gripTestActive;
	private int _gripTestTypeIndex;
	private float _gripTestTime;
	private float _gripTestLogTimer;
	private DrivetrainType _gripTestSavedDrivetrain;
	private DrivetrainType[] _gripTestTypes;
	private GripTestPhase _gripTestPhase;
	private float _gripTestSettleTime;

	private void UpdateGripTest()
	{
		if ( !_gripTestActive || !Engine.IsValid() )
			return;

		// Settling phase: brake to standstill before each type
		if ( _gripTestPhase == GripTestPhase.Settling )
		{
			InputState.direction = new Vector3( -1f, 0f, 0f );
			_gripTestSettleTime += Time.Delta;

			float speed = GetSpeed() * 3.6f;
			if ( speed < 2f || _gripTestSettleTime > 3f )
			{
				_gripTestPhase = GripTestPhase.Running;
				_gripTestTime = 0f;
				_gripTestLogTimer = 0f;

				if ( Rigidbody.IsValid() )
				{
					Rigidbody.Velocity = Vector3.Zero;
					Rigidbody.AngularVelocity = Vector3.Zero;
				}

				Log.Info( $"[GripTest] ═══ {_gripTestTypes[_gripTestTypeIndex]} — GO (full throttle) ═══" );
			}
			return;
		}

		// Running phase: full throttle
		InputState.direction = new Vector3( 1f, 0f, 0f );
		_gripTestTime += Time.Delta;
		_gripTestLogTimer += Time.Delta;

		if ( _gripTestLogTimer >= 0.5f )
		{
			_gripTestLogTimer = 0f;

			var dt = _gripTestTypes[_gripTestTypeIndex];
			float speedKmh = GetSpeed() * 3.6f;
			int gear = Gearbox.IsValid() ? Gearbox.CurrentGear : 0;
			float rpm = Engine.RPM;
			float tcFactor = DbgTractionControl;

			// Per-wheel details
			string wheelDetails = "";
			float totalGrip = 0f;
			float totalFz = 0f;
			float totalSlipR = 0f;
			float totalFx = 0f;
			float totalFy = 0f;
			int wCount = 0;

			foreach ( var w in AllWheels() )
			{
				if ( !w.IsValid() ) continue;
				string driven = "";
				if ( dt == DrivetrainType.FWD && wCount < 2 ) driven = "*";
				else if ( dt == DrivetrainType.RWD && wCount >= 2 ) driven = "*";
				else if ( dt == DrivetrainType.AWD ) driven = "*";

				wheelDetails += $"\n[GripTest]     {w.GameObject.Name}{driven}: Grip={w.Grip:F0} Fz={w.Fz:F0} Fx={w.Fx:F0} Fy={w.Fy:F0} SR={w.SlipRatio:F3} SA={w.DynamicSlipAngle:F2}° w={w.AngularVelocity:F1} Drv={w.DriveTorque:F1}";
				totalGrip += w.Grip;
				totalFz += w.Fz;
				totalSlipR += MathF.Abs( w.SlipRatio );
				totalFx += MathF.Abs( w.Fx );
				totalFy += MathF.Abs( w.Fy );
				wCount++;
			}
			if ( wCount > 0 ) totalSlipR /= wCount;

			Log.Info( $"[GripTest] [{dt}] t={_gripTestTime:F1}s | {speedKmh:F1}km/h G{gear} RPM={rpm:F0} | TC={tcFactor:F2} | TotalGrip={totalGrip:F0} AvgSlip={totalSlipR:F3} TotalFx={totalFx:F0} TotalFy={totalFy:F0} TotalFz={totalFz:F0}{wheelDetails}" );
		}

		// Type done
		if ( _gripTestTime >= GripTestDuration )
		{
			float finalSpeed = GetSpeed() * 3.6f;
			Log.Info( $"[GripTest] ═══ {_gripTestTypes[_gripTestTypeIndex]} DONE — Final speed: {finalSpeed:F1}km/h ═══" );

			_gripTestTypeIndex++;
			if ( _gripTestTypeIndex >= _gripTestTypes.Length )
			{
				// All types done
				Engine.Drivetrain = _gripTestSavedDrivetrain;
				_gripTestActive = false;
				InputState.direction = Vector3.Zero;
				Log.Info( "══════════════════════════════════════════════════════════" );
				Log.Info( $"[GripTest] ALL TYPES COMPLETE — Drivetrain restored to {_gripTestSavedDrivetrain}" );
				Log.Info( "══════════════════════════════════════════════════════════" );
			}
			else
			{
				// Next type
				Engine.Drivetrain = _gripTestTypes[_gripTestTypeIndex];
				_gripTestPhase = GripTestPhase.Settling;
				_gripTestSettleTime = 0f;
				Log.Info( $"[GripTest] ═══ PHASE {_gripTestTypeIndex + 1}/3: {_gripTestTypes[_gripTestTypeIndex]} — Settling... ═══" );
			}
		}
	}

	/// <summary>Recalcule le poids des storages et met à jour la masse du rigidbody.</summary>
	private void UpdateStorageWeight()
	{
		float weight = 0f;
		foreach ( var s in GetComponentsInChildren<VehicleStorage>() )
		{
			if ( s.IsValid() && s.Container.IsValid() )
				weight += s.Container.CurrentWeight;
		}

		StorageWeight = weight;
		Rigidbody.MassOverride = TotalMass;
	}

	/// <summary>Convenience: get all wheels from all axles.</summary>
	public IEnumerable<Wheel> AllWheels()
	{
		if ( Axles == null ) yield break;
		foreach ( var axle in Axles )
		{
			if ( !axle.IsValid() ) continue;
			if ( axle.Left.IsValid() ) yield return axle.Left;
			if ( axle.Right.IsValid() ) yield return axle.Right;
		}
	}

	/// <summary>Returns forward speed in m/s (positive = forward, negative = reversing).</summary>
	public float GetSpeed()
	{
		if ( !Rigidbody.IsValid() ) return 0f;
		// Project velocity onto the vehicle's forward direction, convert inches to meters
		var localVel = WorldRotation.Inverse * Rigidbody.Velocity;
		return localVel.x.InchToMeter(); // s&box: X is forward in local space
	}

	/// <summary>Returns the average contact normal of all grounded wheels.</summary>
	private Vector3 GetAverageContactNormal()
	{
		var sum = Vector3.Zero;
		int count = 0;
		foreach ( var w in AllWheels() )
		{
			if ( !w.IsGrounded ) continue;
			sum += w.ContactNormal;
			count++;
		}
		return count > 0 ? (sum / count).Normal : Vector3.Up;
	}

	/// <summary>
	/// Computes the differential input velocity from driven wheels (lodzero exact copy).
	/// Uses Engine.Differential.GetInputShaftVelocity per drivetrain type.
	/// </summary>
	private float GetDiffInputVelocity()
	{
		if ( Axles == null || Axles.Count < 2 || !Engine.IsValid() ) return 0f;

		var diff = Engine.Differential;

		switch ( Drivetrain )
		{
			case DrivetrainType.FWD:
				return diff.GetInputShaftVelocity(
					Axles[0].Left.IsValid() ? Axles[0].Left.AngularVelocity : 0f,
					Axles[0].Right.IsValid() ? Axles[0].Right.AngularVelocity : 0f );

			case DrivetrainType.RWD:
				return diff.GetInputShaftVelocity(
					Axles[1].Left.IsValid() ? Axles[1].Left.AngularVelocity : 0f,
					Axles[1].Right.IsValid() ? Axles[1].Right.AngularVelocity : 0f );

			case DrivetrainType.AWD:
			{
				float frontVel = diff.GetInputShaftVelocity(
					Axles[0].Left.IsValid() ? Axles[0].Left.AngularVelocity : 0f,
					Axles[0].Right.IsValid() ? Axles[0].Right.AngularVelocity : 0f );
				float rearVel = diff.GetInputShaftVelocity(
					Axles[1].Left.IsValid() ? Axles[1].Left.AngularVelocity : 0f,
					Axles[1].Right.IsValid() ? Axles[1].Right.AngularVelocity : 0f );

				bool frontMoving = MathF.Abs( frontVel ) > 1.0f;
				bool rearMoving = MathF.Abs( rearVel ) > 1.0f;

				if ( frontMoving && rearMoving ) return (frontVel + rearVel) * 0.5f;
				if ( frontMoving ) return frontVel;
				if ( rearMoving ) return rearVel;
				return 0f;
			}

			default:
				return 0f;
		}
	}

	// (Differential torque split is handled by Engine.Differential.GetOutputTorque)

	// ── Main Physics Loop ─────────────────────────────────────────────────────

	protected override void OnFixedUpdate()
	{
		if ( !Rigidbody.IsValid() )
			return;

		// ── Non-host clients: visual wheel updates only (no physics forces) ──
		if ( !Networking.IsHost )
		{
			foreach ( var axle in Axles ?? Enumerable.Empty<Axle>() )
			{
				if ( !axle.IsValid() ) continue;
				axle.Step( visualOnly: true );
			}
			return;
		}

		// ── Host: full physics simulation ────────────────────────────────────
		var rb = Rigidbody;

		// Capture sleeping state BEFORE anything can wake the body
		bool wasSleeping = rb.PhysicsBody is { Sleeping: true };

		// Update storage weight and rigidbody mass
		UpdateStorageWeight();

		// Update odometer and engine wear
		UpdateOdometer();

		// Update fuel persistence
		UpdateFuel();

		// Suspension log runs always (even without driver) to catch sleep/wake
		UpdateSuspensionLog();

		// Don't apply gravity while sleeping — it would wake the body
		if ( !wasSleeping )
			rb.ApplyForce( Vector3.Down * 1000.0f * TotalMass * Downforce );

		// ── Inertia tensor (re-apply each tick in case mass changes) ──────────
		if ( _baseInertia.Length > 0f && InertiaTensorScale != Vector3.One )
		{
			rb.PhysicsBody.SetInertiaTensor( _baseInertia * InertiaTensorScale, Rotation.Identity );
		}

		// Determine if any driver is seated
		var hasDriver = Seats.Any( x => x.HasInput && x.Player.IsValid() );

		// Diagnostic tests override input
		UpdateAccelTest();
		UpdateBrakeTest();
		UpdateCornerTest();
		UpdateSuspensionTest();
		UpdateFuelTest();
		UpdateGripTest();
		if ( _accelTestActive || _brakeTestActive || _cornerTestActive || _suspTestActive || _fuelTestActive || _gripTestActive )
			hasDriver = true;

		// If no driver is seated, reset inputs, step wheels for suspension, let car coast
		if ( !hasDriver )
		{
			InputState.Reset();

			float totalSpeed = rb.Velocity.Length;
			bool allGrounded = AllWheels().Any() && AllWheels().All( w => w.IsGrounded );

			if ( totalSpeed < 3f && allGrounded )
			{
				// Don't force-sleep if a trailer is attached — waking up with
				// overlapping colliders causes a physics explosion.
				var hasTrailer = GameObject.Root.Components.GetInDescendantsOrSelf<TrailerHitch>() is TrailerHitch th
					&& th.IsConnected;

				if ( !hasTrailer )
				{
					// Car is settled — sleep the physics body, no suspension stepping
					rb.Velocity = Vector3.Zero;
					rb.AngularVelocity = Vector3.Zero;
					if ( rb.PhysicsBody is not null )
						rb.PhysicsBody.Sleeping = true;
					return;
				}
			}

			// Car still moving — step wheels for suspension while coasting
			foreach ( var axle in Axles ?? Enumerable.Empty<Axle>() )
			{
				if ( !axle.IsValid() ) continue;
				if ( axle.Left.IsValid() ) { axle.Left.DriveTorque = 0f; axle.Left.BrakeTorque = MaxBrakeTorque; }
				if ( axle.Right.IsValid() ) { axle.Right.DriveTorque = 0f; axle.Right.BrakeTorque = MaxBrakeTorque; }
				axle.Step();
			}
			return;
		}

		// Log once when driver first enters
		if ( !_loggedDriverEnter )
		{
			_loggedDriverEnter = true;
			if ( ShowDebugLogs )
			{
				var wheelCount = AllWheels().Count();
				var axleCount = Axles?.Count ?? 0;
				Log.Info( $"[Vehicle] Driver entered! Axles={axleCount} Wheels={wheelCount} Engine={Engine?.IsValid()} Gearbox={Gearbox?.IsValid()} RB={rb?.IsValid()} Vel={rb?.Velocity.Length:F1}" );
				foreach ( var w in AllWheels() )
					Log.Info( $"[Vehicle]   Wheel: {w.GameObject.Name} pos={w.WorldPosition} grounded={w.IsGrounded} Fz={w.Fz:F0} RestLen={w.RestLength}m" );
			}
		}

		// Wake the rigidbody
		if ( wasSleeping )
		{
			rb.PhysicsBody.Sleeping = false;

			// Reset LastLength to current compressed length to prevent damper spike
			foreach ( var w in AllWheels() )
				w.LastLength = w.RestLength * ( 1f - w.Compression );
		}

		// ── Input ─────────────────────────────────────────────────────────────
		var verticalInput = InputState.direction.x; // +1 = forward, -1 = backward
		float speedMs = GetSpeed(); // m/s, signed (positive = forward)
		float absSpeedMs = MathF.Abs( speedMs );
		int speedDir = speedMs > 0.5f ? 1 : (speedMs < -0.5f ? -1 : 0);
		int inputDir = verticalInput > 0.1f ? 1 : (verticalInput < -0.1f ? -1 : 0);

		var wheelRadius = Axles?.FirstOrDefault()?.Left?.Radius ?? 0.35f;

		DbgEngineValid  = Engine.IsValid();
		DbgGearboxValid = Gearbox.IsValid();

		if ( Engine.IsValid() && Gearbox.IsValid() )
		{
			// ── Gearbox direction ─────────────────────────────────────────
			bool pressingBackward = verticalInput < -0.1f;
			bool pressingForward = verticalInput > 0.1f;

			if ( !pressingBackward ) _wasBackwardReleased = true;
			if ( !pressingForward ) _wasForwardReleased = true;

			if ( pressingBackward && !Gearbox.IsReverse )
			{
				if ( absSpeedMs < 0.5f && _wasBackwardReleased )
					Gearbox.SetReverse();
				_wasBackwardReleased = false;
			}
			else if ( pressingForward && Gearbox.IsReverse )
			{
				if ( absSpeedMs < 0.5f && _wasForwardReleased )
					Gearbox.SetDrive();
				_wasForwardReleased = false;
			}

			// ── Smooth throttle / brake (lodzero style) ───────────────────
			// Braking detection: input direction opposes travel direction
			bool braking = inputDir != 0 && inputDir != speedDir && speedDir != 0;

			float targetThrottle;
			float targetBrake;

			if ( braking )
			{
				targetThrottle = 0f;
				targetBrake = MathF.Abs( verticalInput );
			}
			else if ( Gearbox.IsReverse )
			{
				targetThrottle = verticalInput < 0f ? -verticalInput : 0f;
				targetBrake = 0f;
			}
			else
			{
				targetThrottle = verticalInput > 0f ? verticalInput : 0f;
				targetBrake = 0f;
			}

			// Véhicule immobilisé → pas d'accélération
			if ( Internals.IsValid() && Internals.IsImmobilized )
				targetThrottle = 0f;

			_smoothThrottle = MathX.Lerp( _smoothThrottle, targetThrottle, Time.Delta * ThrottleSpeed );
			_smoothBrake = MathX.Lerp( _smoothBrake, targetBrake, Time.Delta * ThrottleSpeed * 2.0f );

			// Handbrake: 0 or 1 from input, smoothed slightly
			float targetHandbrake = InputState.isHandbraking ? 1.0f : 0.0f;
			_smoothHandbrake = MathX.Lerp( _smoothHandbrake, targetHandbrake, Time.Delta * 10.0f );
			IsHandbrakeActive = _smoothHandbrake > 0.1f;

			// ── Brake torques with bias ───────────────────────────────────
			float frontBrakeTorque = MaxBrakeTorque * _smoothBrake * BrakeBias;
			float rearFootBrake = MaxBrakeTorque * _smoothBrake * (1.0f - BrakeBias);

			// ── ABS (only affects foot brake, not handbrake) ─────────────
			if ( _smoothBrake > 0.05f && _smoothThrottle < 0.1f && ABS > 0f && !IsHandbrakeActive )
			{
				float frontSlipAbs = 0f;
				float rearSlipAbs = 0f;

				if ( Axles != null && Axles.Count >= 2 )
				{
					var front = Axles[0];
					var rear = Axles[1];
					if ( front.IsValid() )
					{
						if ( front.Left.IsValid() ) frontSlipAbs = MathF.Max( frontSlipAbs, MathF.Abs( front.Left.SlipRatio ) );
						if ( front.Right.IsValid() ) frontSlipAbs = MathF.Max( frontSlipAbs, MathF.Abs( front.Right.SlipRatio ) );
					}
					if ( rear.IsValid() )
					{
						if ( rear.Left.IsValid() ) rearSlipAbs = MathF.Max( rearSlipAbs, MathF.Abs( rear.Left.SlipRatio ) );
						if ( rear.Right.IsValid() ) rearSlipAbs = MathF.Max( rearSlipAbs, MathF.Abs( rear.Right.SlipRatio ) );
					}
				}

				// Proportional ABS: reduce brake force proportionally to how much slip exceeds threshold
				const float absSlipThreshold = 0.3f;
				const float absSlipMax = 1.0f;

				if ( frontSlipAbs > absSlipThreshold )
				{
					float excess = (frontSlipAbs - absSlipThreshold) / (absSlipMax - absSlipThreshold);
					_absFrontMod = MathX.Clamp( 1.0f - excess, 0.2f, 1.0f );
				}
				else
				{
					_absFrontMod = MathX.Lerp( _absFrontMod, 1.0f, Time.Delta * ABS );
				}

				if ( rearSlipAbs > absSlipThreshold )
				{
					float excess = (rearSlipAbs - absSlipThreshold) / (absSlipMax - absSlipThreshold);
					_absRearMod = MathX.Clamp( 1.0f - excess, 0.2f, 1.0f );
				}
				else
				{
					_absRearMod = MathX.Lerp( _absRearMod, 1.0f, Time.Delta * ABS );
				}

				frontBrakeTorque *= _absFrontMod;
				rearFootBrake *= _absRearMod;
			}
			else
			{
				_absFrontMod = 1.0f;
				_absRearMod = 1.0f;
			}

			// Handbrake adds to rear brake torque AFTER ABS (handbrake bypasses ABS)
			float handBrakeForce = MaxHandBrakeTorque * _smoothHandbrake;
			float rearBrakeTorque = rearFootBrake + handBrakeForce;

			DbgABS = (_absFrontMod + _absRearMod) * 0.5f;

			// ── Clutch torque (lodzero exact copy) ────────────────────────
			float diffInputVel = GetDiffInputVelocity();
			float gearboxInputVel = Engine.Gearbox.IsValid()
				? Engine.Gearbox.GetInputShaftVelocity( diffInputVel )
				: 0f;
			float gearboxRatio = Engine.Gearbox.IsValid()
				? Engine.Gearbox.GetCurrentRatio()
				: 0f;

			bool isShifting = Engine.Gearbox.IsValid() && Engine.Gearbox.IsShifting;
			Engine.Clutch.Update( Time.Delta, gearboxInputVel, Engine.AngularVelocity,
				gearboxRatio, _smoothThrottle, Engine.RPM, isShifting );

			float clutchTorque = Engine.Clutch.ClutchTorque;

			// Disable clutch torque if all wheels are stopped (lodzero exact)
			bool allWheelsStopped = true;
			foreach ( var axle in Axles ?? Enumerable.Empty<Axle>() )
			{
				if ( !axle.IsValid() ) continue;
				if ( (axle.Left.IsValid() && MathF.Abs( axle.Left.AngularVelocity ) >= 0.5f) ||
					 (axle.Right.IsValid() && MathF.Abs( axle.Right.AngularVelocity ) >= 0.5f) )
				{
					allWheelsStopped = false;
					break;
				}
			}
			if ( allWheelsStopped )
				Engine.Clutch.ClutchTorque = 0f;

			DbgClutchTorque = clutchTorque;

			// ── Traction Control ──────────────────────────────────────────
			const float tcSlipThreshold = 0.4f;
			const float tcActivationMargin = 0.15f;

			if ( TractionControl > 0f && _smoothThrottle > 0.05f && _smoothBrake < 0.1f )
			{
				float maxSlip = 0f;
				float frontSlip = 0f;
				float rearSlip = 0f;

				if ( Axles != null && Axles.Count >= 2 )
				{
					if ( Axles[0].IsValid() )
					{
						if ( Axles[0].Left.IsValid() ) frontSlip = MathF.Max( frontSlip, MathF.Abs( Axles[0].Left.SlipRatio ) );
						if ( Axles[0].Right.IsValid() ) frontSlip = MathF.Max( frontSlip, MathF.Abs( Axles[0].Right.SlipRatio ) );
					}
					if ( Axles[1].IsValid() )
					{
						if ( Axles[1].Left.IsValid() ) rearSlip = MathF.Max( rearSlip, MathF.Abs( Axles[1].Left.SlipRatio ) );
						if ( Axles[1].Right.IsValid() ) rearSlip = MathF.Max( rearSlip, MathF.Abs( Axles[1].Right.SlipRatio ) );
					}
				}

				maxSlip = Drivetrain switch
				{
					DrivetrainType.FWD => frontSlip,
					DrivetrainType.RWD => rearSlip,
					_ => MathF.Max( frontSlip, rearSlip )
				};

				float targetTc = _tractionControlFactor;
				if ( maxSlip > tcSlipThreshold + tcActivationMargin )
				{
					float slipExcess = (maxSlip - tcSlipThreshold) / (1f + tcSlipThreshold);
					targetTc = 1f - slipExcess.Clamp( 0f, 0.7f );
				}
				else
				{
					targetTc = 1f;
				}

				// Smooth interpolation — cut faster than recovery to avoid oscillation
				float cutSpeed = TractionControl * 2f;
				float recoverSpeed = TractionControl * 0.5f;
				float speed = targetTc < _tractionControlFactor ? cutSpeed : recoverSpeed;
				_tractionControlFactor = MathX.Lerp( _tractionControlFactor, targetTc, speed * Time.Delta );
				_tractionControlFactor = _tractionControlFactor.Clamp( 0.3f, 1.0f );
			}

			DbgTractionControl = _tractionControlFactor;

			// ── Gearbox output torque ─────────────────────────────────────
			float gearTorque = Engine.Gearbox.IsValid()
				? Engine.Gearbox.GetOutputTorque( clutchTorque ) * _tractionControlFactor
				: 0f;

			// ── Differential torque distribution (lodzero exact copy) ─────
			Vector2 frontDiffTorque = Vector2.Zero;
			Vector2 rearDiffTorque = Vector2.Zero;

			switch ( Drivetrain )
			{
				case DrivetrainType.FWD:
					frontDiffTorque = Engine.Differential.GetOutputTorque( gearTorque );
					break;
				case DrivetrainType.RWD:
					rearDiffTorque = Engine.Differential.GetOutputTorque( gearTorque );
					break;
				case DrivetrainType.AWD:
					frontDiffTorque = Engine.Differential.GetOutputTorque( gearTorque * 0.5f );
					rearDiffTorque = Engine.Differential.GetOutputTorque( gearTorque * 0.5f );
					break;
			}

			// ── Apply to axles (lodzero exact: front=0, rear=1) ──────────
			if ( Axles != null && Axles.Count >= 2 )
			{
				ApplyAxleTorque( Axles[0], frontDiffTorque, frontBrakeTorque );
				ApplyAxleTorque( Axles[1], rearDiffTorque, rearBrakeTorque );
			}

			// ── Engine update (lodzero: engine gets MathF.Abs(Throttle)) ──
			Engine.UpdateEngine( Time.Delta, MathF.Abs( _smoothThrottle ), clutchTorque, GetSpeed(), _smoothBrake );

			// ── Gearbox auto shift ────────────────────────────────────────
			if ( Engine.Gearbox.IsValid() )
			{
				Engine.Gearbox.Update( Engine.RPM, verticalInput, GetSpeed(), Time.Delta );

				// Shift kick: small forward impulse when gear engages (simulates drivetrain jolt)
				if ( _wasShifting && !Engine.Gearbox.IsShifting )
				{
					int newGear = Engine.Gearbox.CurrentGear;
					float gearRatio = Engine.Gearbox.Ratio;

					if ( newGear >= 1 && Rigidbody.IsValid() )
					{
						float kickStrength = TotalMass * 0.4f * MathF.Abs( gearRatio );
						Rigidbody.ApplyForce( WorldRotation.Forward * kickStrength );
					}
				}

				_wasShifting = Engine.Gearbox.IsShifting;
				_lastGear = Engine.Gearbox.CurrentGear;
			}

			// ── Neutral burnout force (lodzero exact) ─────────────────────
			if ( Engine.Gearbox.IsValid() && Engine.Gearbox.CurrentGear == 0 )
			{
				Rigidbody.ApplyForceAt(
					WorldPosition + WorldRotation.Right * 3f + WorldRotation.Forward * 5f,
					Vector3.Up * Engine.RPM.MapRange( 0f, 7000f, 0f, TotalMass * 75f ) * -1f * verticalInput
				);
			}

			// Apply forced induction (turbo/supercharger) if present — updates boost state
			// (boost is applied to engine torque internally, not to wheel torque)

			DbgThrottle     = _smoothThrottle;
			DbgBrakeInput   = _smoothBrake;
			DbgCarSpeed     = absSpeedMs;
			DbgEngineTorque = Engine.GetEffectiveTorque();
		}
		else
		{
			// No engine/gearbox: just step axles with no torque
			foreach ( var axle in Axles ?? Enumerable.Empty<Axle>() )
			{
				if ( !axle.IsValid() ) continue;
				if ( axle.Left.IsValid() ) { axle.Left.DriveTorque = 0f; axle.Left.BrakeTorque = 0f; }
				if ( axle.Right.IsValid() ) { axle.Right.DriveTorque = 0f; axle.Right.BrakeTorque = 0f; }
				axle.Step();
			}
		}

		// ── Rest damping: reduce jitter when nearly stopped ──────────────────
		if ( MathF.Abs( verticalInput ) < 0.05f && absSpeedMs < 0.3f
			&& AllWheels().All( w => w.IsGrounded ) )
		{
			rb.Velocity = rb.Velocity * 0.95f;
			rb.AngularVelocity = rb.AngularVelocity * 0.95f;
		}

		// ── Handbrake hold: aggressively stop the vehicle when handbrake is held ──
		if ( IsHandbrakeActive && absSpeedMs < 2.0f && AllWheels().All( w => w.IsGrounded ) )
		{
			float brakeDamp = _smoothHandbrake * 0.85f;
			rb.Velocity = rb.Velocity * (1f - brakeDamp);
			rb.AngularVelocity = rb.AngularVelocity * (1f - brakeDamp * 0.5f);
		}

		// ── Auto-right ────────────────────────────────────────────────────────
		var currentUp = WorldRotation.Up;
		var alignment = Vector3.Dot( Vector3.Up, currentUp );

		if ( alignment < 0.1f )
		{
			var desiredRotation = Rotation.From( 0, WorldRotation.Angles().yaw, 0 );
			var rotationDifference = desiredRotation * WorldRotation.Inverse;

			ToAngleAxis( rotationDifference, out var angle, out var axis );
			angle = MathF.Min( angle, 180f );
			rb.AngularVelocity = axis * angle * 1.5f * Time.Delta;
		}
	}

	/// <summary>
	/// Applies drive torque (as Vector2 left/right from differential) and brake torque
	/// to a single axle, then steps the axle physics.
	/// </summary>
	private void ApplyAxleTorque( Axle axle, Vector2 diffTorque, float brakeTorque )
	{
		if ( !axle.IsValid() ) return;

		if ( axle.Left.IsValid() )
		{
			axle.Left.DriveTorque = diffTorque.x * axle.Left.DrivingRatio;
			axle.Left.BrakeTorque = brakeTorque;
		}
		if ( axle.Right.IsValid() )
		{
			axle.Right.DriveTorque = diffTorque.y * axle.Right.DrivingRatio;
			axle.Right.BrakeTorque = brakeTorque;
		}

		axle.Step();
	}

	// ── Utilities ─────────────────────────────────────────────────────────────

	/// <summary>Converts a quaternion to angle-axis representation.</summary>
	private void ToAngleAxis( Rotation rotation, out float angle, out Vector3 axis )
	{
		var normalized = rotation.Normal;
		angle = 2.0f * (float)Math.Acos( normalized.w );
		var sinThetaOver2 = (float)Math.Sqrt( 1.0f - normalized.w * normalized.w );

		if ( sinThetaOver2 > 0.0001f )
			axis = new Vector3( normalized.x, normalized.y, normalized.z ) / sinThetaOver2;
		else
			axis = new Vector3( 1, 0, 0 );

		axis = axis.Normal;
		angle = angle * (180.0f / (float)Math.PI);
	}

	// ── OnUpdate (debug HUD) ──────────────────────────────────────────────────

	/// <summary>Distance max autour du bounding box du véhicule pour ouvrir le radial.</summary>
	private const float RadialInteractionMargin = 60f;

	protected override void OnUpdate()
	{
		// Propagate gizmo toggle to all wheels
		foreach ( var wheel in AllWheels() )
			wheel.ShowForceGizmos = ShowDebugGizmos;

		if ( ShowDebugHUD )
			DrawDebugHUD();

		// Détection locale : ouvrir le radial si le joueur est proche du véhicule et appuie E
		if ( !Input.Pressed( "Use" ) ) return;

		var pawn = Game.ActiveScene?.GetAllComponents<OpenFramework.Systems.Pawn.PlayerPawn>()
			.FirstOrDefault( x => !x.IsProxy );
		if ( pawn == null ) return;

		// Don't open exterior radial if the player is inside this vehicle
		if ( pawn.CurrentCar.IsValid() ) return;

		var playerPos = pawn.WorldPosition;
		var bounds = GameObject.GetBounds();
		var expanded = new BBox( bounds.Mins - RadialInteractionMargin, bounds.Maxs + RadialInteractionMargin );

		if ( expanded.Contains( playerPos ) )
		{
			UI.VehicleRadialMenu.Open( this );
		}
	}

	private void DrawDebugHUD()
	{
		DebugText.Update();
		var rb = Rigidbody;
		var kmh = DbgCarSpeed * 3.6f;
		var hasDriver = Seats.Any( s => s.HasInput && s.Player.IsValid() );

		// ── Inputs ──
		var dir = InputState.direction;
		var steerInput = dir.y;
		var accelInput = dir.x;
		DebugText.Write( $"Input: Accel:{accelInput:F2} Steer:{steerInput:F2} Boost:{InputState.isBoosting} Handbrake:{InputState.isHandbraking}", Color.Orange );
		DebugText.Spacer();

		// ── Vehicle State ──
		var engineName = Engine?.Preset?.DisplayName ?? "Custom";
		DebugText.Write( $"Speed: {DbgCarSpeed:F1} m/s ({kmh:F0} km/h) | {engineName} | Mass: {TotalMass:F0}kg ({Mass:F0}+{StorageWeight:F1}) | Downforce: {Downforce:F1}x | Odo: {Odometer:F1}km | Wear: {WearPowerFactor:P0}", Color.Green );
		DebugText.Write( $"Throttle: {DbgThrottle:F2} | TC Factor: {DbgTractionControl:F2}", Color.White );
		DebugText.Write( $"Brake: {DbgBrakeInput:F2} | ABS: {DbgABS:F2} (F:{_absFrontMod:F2} R:{_absRearMod:F2})", Color.Red );
		DebugText.Write( $"Clutch Torque: {DbgClutchTorque:F1} | Lock: {Engine?.Clutch.ClutchLock:F2}", Color.Yellow );
		DebugText.Write( $"RPM: {Engine?.RPM.FloorToInt() ?? 0} / {Engine?.MaxRPM.FloorToInt() ?? 0}", Color.Yellow );
		var gearRatio = Engine?.Gearbox?.GetCurrentRatio() ?? 0f;
		var diffRatio = Engine?.DifferentialRatio ?? 0f;
		DebugText.Write( $"Gear: {Engine?.Gearbox?.GearLabel ?? "-"} | Ratio: {gearRatio:F2} x {diffRatio:F2} | Up:{Engine?.ShiftUpRPM:F0} Down:{Engine?.ShiftDownRPM:F0} | Shifting:{Engine?.Gearbox?.IsShifting}", Color.White );
		DebugText.Write( $"Torque: {DbgEngineTorque:F0} | HBRK: {(IsHandbrakeActive ? "ON" : "OFF")} | TC: {TractionControl:F1} | ABS: {ABS:F1}", Color.White );

		if ( Gearbox.IsValid() )
		{
			var gearboxName = Gearbox.Preset?.DisplayName ?? "Custom";
			DebugText.Write( $"Gearbox: {gearboxName}", Color.Cyan );
		}

		if ( ForcedInduction.IsValid() && ForcedInduction.Enabled )
		{
			var fi = ForcedInduction;
			float boostPct = fi.BoostNormalized * 100f;
			float multiplier = fi.GetTorqueMultiplier();
			var boostColor = boostPct > 50f ? Color.Magenta : Color.White;
			DebugText.Write( $"Boost: {fi.CurrentBoost:F2} bar ({boostPct:F0}%) | x{multiplier:F2} | {fi.Type}", boostColor );
		}

		if ( Engine != null )
		{
			var presetName = Engine.Preset?.DisplayName ?? "Custom";
			DebugText.Write( $"Engine: {presetName} | {(hasDriver ? "DRIVER" : "PARKED")}", Color.Cyan );
		}

		if ( rb.IsValid() )
		{
			var angles = rb.WorldRotation.Angles();
			DebugText.Write( $"Roll: {angles.roll:F1} Pitch: {angles.pitch:F1}", Color.White );
			DebugText.Write( $"Diff: {Engine?.Diff} | DiffRatio: {Engine?.DifferentialRatio:F2} | Drivetrain: {Drivetrain}", Color.White );
			DebugText.Write( $"Inertia Scale: {InertiaTensorScale}", Color.White );
		}

		DebugText.Spacer();

		foreach ( var wheel in AllWheels() )
		{
			var name = wheel.GameObject.Name;
			DebugText.Write( $"{name} Fx: {wheel.Fx.FloorToInt()}", Color.White );
			DebugText.Write( $"{name} Fy: {wheel.Fy.FloorToInt()}", Color.White );
			DebugText.Write( $"{name} Fz: {wheel.Fz.FloorToInt()}", Color.Cyan );
			DebugText.Write( $"{name} Slip X: {wheel.DynamicSlipRatio:F2}", wheel.DynamicSlipRatio > 0.15f ? Color.Red : Color.Green );
			DebugText.Write( $"{name} Slip Y: {wheel.DynamicSlipAngle:F2}", MathF.Abs( wheel.DynamicSlipAngle ) > 5f ? Color.Red : Color.Green );
			DebugText.Write( $"{name} AVel: {wheel.AngularVelocity:F1}", Color.White );
			DebugText.Spacer();
		}
	}

	// ── Interface Implementations ─────────────────────────────────────────────

	public void OnKill( OpenFramework.Systems.Pawn.DamageInfo damageInfo )
	{
		foreach ( var seat in Seats )
		{
			seat.Eject();
		}

		Explosion?.Clone( WorldPosition );
		GameObject.Destroy();
	}

	public UseResult CanUse( PlayerPawn player )
	{
		// Le radial est géré par OnUpdate — IUse ne doit pas interférer
		return false;
	}

	public void OnUse( PlayerPawn player )
	{
		// Le radial menu est maintenant géré par OnUpdate (détection bounding box côté client)
		// OnUse n'ouvre plus le radial pour éviter les doubles appels
	}

	/// <summary>Ouvre le menu radial véhicule côté client.</summary>
	[Rpc.Broadcast]
	private void OpenVehicleMenuClient()
	{
		Log.Info( $"[Vehicle] OpenVehicleMenuClient — received on client" );
		VehicleRadialMenu.Open( this );
	}

	/// <summary>Demande au serveur d'entrer dans le siège le plus proche.</summary>
	[Rpc.Host]
	public void RequestEnterSeat()
	{
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		var closestSeat = Seats
			.Where( x => x.CanEnter( pawn ) )
			.OrderBy( x => x.WorldPosition.DistanceSquared( pawn.WorldPosition ) )
			.FirstOrDefault();

		if ( ShowDebugLogs )
		{
			if ( closestSeat == null )
				Log.Warning( $"[Vehicle] No available seat found!" );
			else
				Log.Info( $"[Vehicle] Entering seat: {closestSeat.GameObject.Name} (HasInput={closestSeat.HasInput})" );
		}

		closestSeat?.Enter( pawn );
	}

	/// <summary>Demande au serveur de sortir du siège.</summary>
	[Rpc.Host]
	public void RequestExitSeat()
	{
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		if ( pawn == null ) return;

		var seat = Seats?.FirstOrDefault( s => s.Player == pawn );
		seat?.Leave( pawn );
	}

	/// <summary>Trouve le VehicleInspectionPoint le plus proche du joueur et déclenche l'inspection.</summary>
	[Rpc.Host]
	public void RequestInspectNearest()
	{
		Log.Info( $"[Vehicle] RequestInspectNearest called" );
		var pawn = Rpc.Caller.GetClient()?.PlayerPawn as PlayerPawn;
		Log.Info( $"[Vehicle] RequestInspectNearest — pawn={pawn?.GameObject?.Name}" );
		if ( pawn == null ) return;

		var closest = Components.GetAll<VehicleInspectionPoint>( FindMode.EverythingInDescendants )
			.OrderBy( p => p.WorldPosition.DistanceSquared( pawn.WorldPosition ) )
			.FirstOrDefault();

		Log.Info( $"[Vehicle] RequestInspectNearest — closest={closest?.ComponentType} dist={closest?.WorldPosition.Distance( pawn.WorldPosition ):F0}" );
		if ( closest == null ) return;

		closest.DoInspection( pawn );
		Log.Info( $"[Vehicle] RequestInspectNearest — DoInspection done" );
	}

	public void OnDamaged( OpenFramework.Systems.Pawn.DamageInfo damageInfo )
	{
		if ( HealthComponent.Health < FireThreshold && !_fire.IsValid() )
		{
			_fire = Fire.Clone( GameObject, Vector3.Zero, Rotation.Identity, Vector3.One );
		}
	}

	protected override void OnDestroy()
	{
		// Sauvegarder le kilométrage et carburant avant destruction
		SaveOdometer();
		SaveFuel();

		if ( _fire.IsValid() )
			_fire.Destroy();
	}
}
