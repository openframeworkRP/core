namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Automatic gearbox with shift timing, ported from lodzero's Gearbox.
/// During a shift, the gear ratio goes to 0 (clutch disengaged) for ShiftTime seconds,
/// then the new gear engages. This creates realistic power interruption during shifts.
/// </summary>
[Category( "Vehicles" )]
[Title( "Gearbox" )]
[Icon( "swap_vert" )]
public sealed class Gearbox : Component
{
	// ── Preset ────────────────────────────────────────────────────────────────

	/// <summary>Optional gearbox preset. When assigned, click "Apply Preset" to copy its values.</summary>
	[Property, Group( "Preset" )] public GearboxPreset Preset { get; set; }

	/// <summary>Copies all values from the assigned preset into this gearbox's properties.</summary>
	[Button( "Apply Preset" )]
	public void ApplyPreset()
	{
		if ( Preset == null )
		{
			Log.Warning( "Gearbox: No preset assigned." );
			return;
		}

		ForwardGearRatios = new List<float>( Preset.ForwardGearRatios );
		ReverseGearRatio  = Preset.ReverseGearRatio;
		ShiftUpRpm        = Preset.UpshiftRpm;
		ShiftDownRpm      = Preset.DownshiftRpm;
		ShiftDelay        = Preset.ShiftCooldown;
		ShiftTime         = Preset.ShiftTime;

		Log.Info( $"Gearbox: Applied preset '{Preset.DisplayName}'" );
	}

	// ── Ratios ────────────────────────────────────────────────────────────────

	/// <summary>Forward gear ratios from 1st to Nth gear.</summary>
	[Property, Group( "Ratios" )]
	public List<float> ForwardGearRatios { get; set; } = new() { 3.8f, 2.0f, 1.45f, 1.10f, 0.87f };

	/// <summary>Gear ratio used in reverse (stored as positive, sign applied internally).</summary>
	[Property, Group( "Ratios" )] public float ReverseGearRatio { get; set; } = 3.818f;

	// ── Auto shift ────────────────────────────────────────────────────────────

	/// <summary>Whether the gearbox shifts automatically.</summary>
	[Property, Group( "Auto Shift" )] public bool IsAutomatic { get; set; } = true;

	/// <summary>RPM at which the gearbox shifts up.</summary>
	[Property, Group( "Auto Shift" )] public float ShiftUpRpm { get; set; } = 6600f;

	/// <summary>RPM at which the gearbox shifts down.</summary>
	[Property, Group( "Auto Shift" )] public float ShiftDownRpm { get; set; } = 2500f;

	/// <summary>Minimum time in seconds between two consecutive shifts.</summary>
	[Property, Group( "Shift Timing" )] public float ShiftDelay { get; set; } = 1.0f;

	/// <summary>Time in seconds the gearbox spends disengaged during a shift (ratio = 0).</summary>
	[Property, Group( "Shift Timing" )] public float ShiftTime { get; set; } = 0.2f;

	/// <summary>Enable detailed gearbox debug logs in the console.</summary>
	[Property, Group( "Debug" )] public bool ShowDebugLogs { get; set; } = false;

	// ── State (synced for passengers HUD, sounds) ────────────────────────────

	/// <summary>Current gear. -1 = reverse, 0 = neutral, 1..N = forward gears.</summary>
	[Sync( SyncFlags.FromHost )] public int CurrentGear { get; private set; } = 1;

	/// <summary>Current effective gear ratio. Goes to 0 during a shift (clutch disengaged).</summary>
	public float Ratio { get; private set; }

	/// <summary>True while the gearbox is mid-shift (ratio = 0, clutch disengaged).</summary>
	public bool IsShifting { get; private set; }

	/// <summary>True when a gear is engaged (not shifting, not neutral).</summary>
	public bool InGear { get; private set; } = true;

	/// <summary>True when in a forward gear (not reverse).</summary>
	public bool InDrive { get; private set; } = true;

	/// <summary>Force the gearbox into neutral regardless of auto-shift logic.</summary>
	public bool ForceNeutral { get; set; }

	// ── Shift timing internals ────────────────────────────────────────────────

	private float _lastShiftTime;
	private float _shiftTimer;
	private int _pendingGear = -999;
	private float _lastRpm;
	private float _lastSpeed;

	// ── Derived properties ────────────────────────────────────────────────────

	public int NumForwardGears => ForwardGearRatios?.Count ?? 0;

	public bool IsReverse => CurrentGear == -1;
	public bool IsNeutral => CurrentGear == 0;

	/// <summary>Display string for HUD (e.g. "R", "N", "1", "2" ...).</summary>
	public string GearLabel => CurrentGear switch
	{
		-1 => "R",
		0  => "N",
		_  => CurrentGear.ToString()
	};

	// ── Initialization ────────────────────────────────────────────────────────

	protected override void OnEnabled()
	{
		SetGearImmediate( 1 );
	}

	// ── Update (called by Engine each physics tick) ───────────────────────────

	/// <summary>
	/// Runs the auto-shift logic and handles in-progress shift timing.
	/// Called by the Engine component each physics tick.
	/// </summary>
	public void Update( float engineRPM, float throttle, float vehicleSpeed, float deltaTime )
	{
		_lastRpm = engineRPM;
		_lastSpeed = vehicleSpeed;

		// Handle in-progress shift timing
		if ( IsShifting )
		{
			_shiftTimer -= deltaTime;
			if ( _shiftTimer <= 0.0f )
			{
				SetGearImmediate( _pendingGear );
				_lastShiftTime = 0.0f; // Cooldown starts when shift completes
			}
			return;
		}

		if ( !IsAutomatic )
			return;

		if ( ForceNeutral )
		{
			BeginShift( 0 );
			return;
		}

		_lastShiftTime += deltaTime;

		const float lowSpeedThreshold = 2.0f; // m/s (~7 km/h)
		const float throttleThreshold = 0.2f;

		// At low speed, handle forward/reverse switching (only from neutral)
		// Vehicle.cs handles the "release and re-press" logic for forward gear ↔ reverse
		if ( MathF.Abs( vehicleSpeed ) < lowSpeedThreshold )
		{
			if ( throttle > throttleThreshold && CurrentGear < 1 )
			{
				SetGearImmediate( 1 );
				InDrive = true;
				_lastShiftTime = 0.0f;
				return;
			}
			if ( throttle < -throttleThreshold && CurrentGear == 0 )
			{
				SetGearImmediate( -1 );
				InDrive = false;
				_lastShiftTime = 0.0f;
				return;
			}
		}

		if ( IsReverse || !InDrive || _lastShiftTime < ShiftDelay )
			return;

		if ( engineRPM > ShiftUpRpm && CurrentGear < NumForwardGears )
		{
			ShiftUp();
			_lastShiftTime = 0.0f;
		}
		else if ( engineRPM < ShiftDownRpm && throttle < 0.8f && CurrentGear > 1 )
		{
			ShiftDown();
			_lastShiftTime = 0.0f;
		}
	}

	// ── Manual shift API ──────────────────────────────────────────────────────

	public void ShiftUp()
	{
		if ( CurrentGear < NumForwardGears && !IsShifting )
			BeginShift( CurrentGear + 1 );
	}

	public void ShiftDown()
	{
		if ( CurrentGear > 1 && !IsShifting )
			BeginShift( CurrentGear - 1 );
	}

	public void SetReverse()
	{
		if ( CurrentGear == -1 ) return;
		SetGearImmediate( -1 );
		InDrive = false;
	}

	public void SetDrive()
	{
		if ( CurrentGear > 0 ) return;
		SetGearImmediate( 1 );
		InDrive = true;
	}

	// ── Internal shift mechanics ──────────────────────────────────────────────

	/// <summary>
	/// Begins a shift with timing delay. Sets ratio to 0 (clutch disengaged)
	/// and waits ShiftTime seconds before completing the shift.
	/// </summary>
	private void BeginShift( int newGear )
	{
		if ( CurrentGear == newGear ) return;

		if ( ShowDebugLogs )
			Log.Info( $"[Gearbox] BeginShift {CurrentGear} → {newGear} | ShiftTime={ShiftTime:F2}s RPM={_lastRpm:F0} Speed={_lastSpeed:F1}" );

		IsShifting = true;
		_shiftTimer = ShiftTime;
		_pendingGear = newGear;
		Ratio = 0.0f; // Clutch disengaged during shift
		InGear = false;
	}

	/// <summary>
	/// Immediately sets the gear without shift timing (used for initial setup and shift completion).
	/// </summary>
	private void SetGearImmediate( int newGear )
	{
		int oldGear = CurrentGear;
		CurrentGear = newGear;

		if ( CurrentGear >= 1 && CurrentGear <= NumForwardGears )
		{
			Ratio = ForwardGearRatios[CurrentGear - 1];
			InGear = true;
			InDrive = true;
		}
		else if ( CurrentGear == -1 )
		{
			Ratio = -ReverseGearRatio;
			InGear = true;
			InDrive = false;
		}
		else
		{
			Ratio = 0.0f; // Neutral
			InGear = false;
		}

		if ( ShowDebugLogs && oldGear != newGear )
			Log.Info( $"[Gearbox] Engaged {GearLabel} | Ratio={Ratio:F3} RPM={_lastRpm:F0} Speed={_lastSpeed:F1}" );

		IsShifting = false;
		_pendingGear = -999;
	}

	// ── Utility ───────────────────────────────────────────────────────────────

	/// <summary>Multiplies input torque by the current gear ratio.</summary>
	public float GetOutputTorque( float inputTorque )
	{
		return inputTorque * Ratio;
	}

	/// <summary>Converts output shaft velocity to input shaft velocity through the gear ratio.</summary>
	public float GetInputShaftVelocity( float outputVelocity )
	{
		return outputVelocity * Ratio;
	}

	/// <summary>Returns the current effective gear ratio (0 during shift).</summary>
	public float GetCurrentRatio()
	{
		return Ratio;
	}
}
