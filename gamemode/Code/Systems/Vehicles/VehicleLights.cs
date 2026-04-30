namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Controls vehicle lights: headlights, brake lights, reverse lights, and turn signals.
/// Toggle headlights with Flashlight key (F).
/// Turn signals: Left arrow = left, Right arrow = right, Ctrl = hazard.
/// Brake/reverse are automatic.
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Lights" )]
[Icon( "lightbulb" )]
public sealed class VehicleLights : Component
{
	[Property, Group( "References" )] public Vehicle Vehicle { get; set; }

	// ── Front lights ──────────────────────────────────────────────────────────

	/// <summary>Front low beam lights (dipped headlights).</summary>
	[Property, Group( "Front" )] public List<GameObject> LowBeams { get; set; }

	/// <summary>Front high beam lights (full headlights).</summary>
	[Property, Group( "Front" )] public List<GameObject> HighBeams { get; set; }

	// ── Rear lights ───────────────────────────────────────────────────────────

	/// <summary>Rear brake lights.</summary>
	[Property, Group( "Rear" )] public List<GameObject> BrakeLights { get; set; }

	/// <summary>Rear reverse lights (white).</summary>
	[Property, Group( "Rear" )] public List<GameObject> ReverseLights { get; set; }

	// ── Turn signals ──────────────────────────────────────────────────────────

	/// <summary>Left turn signal lights (front + rear).</summary>
	[Property, Group( "Turn Signals" )] public List<GameObject> TurnSignalLeft { get; set; }

	/// <summary>Right turn signal lights (front + rear).</summary>
	[Property, Group( "Turn Signals" )] public List<GameObject> TurnSignalRight { get; set; }

	/// <summary>Blink interval in seconds.</summary>
	[Property, Group( "Turn Signals" )] public float BlinkInterval { get; set; } = 0.5f;

	// ── State ─────────────────────────────────────────────────────────────────

	public enum HeadlightState
	{
		Off,
		LowBeam,
		HighBeam
	}

	public enum TurnSignalState
	{
		Off,
		Left,
		Right,
		Hazard
	}

	[Sync( SyncFlags.FromHost )] public HeadlightState Headlights { get; private set; } = HeadlightState.Off;
	[Sync( SyncFlags.FromHost )] public TurnSignalState TurnSignal { get; private set; } = TurnSignalState.Off;

	private TimeSince _blinkTimer;
	private bool _blinkOn;

	/// <summary>Cached reference to the scene's DayNightCycle for auto headlights.</summary>
	private DayNightCycle _dayNight;

	/// <summary>True when headlights were turned on automatically (not by the player).</summary>
	private bool _autoHeadlights;

	protected override void OnStart()
	{
		_dayNight = Scene.GetAllComponents<DayNightCycle>().FirstOrDefault();

		SetAll( LowBeams, false );
		SetAll( HighBeams, false );
		SetAll( BrakeLights, false );
		SetAll( ReverseLights, false );
		SetAll( TurnSignalLeft, false );
		SetAll( TurnSignalRight, false );
	}

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() )
			return;

		// ── Handle input (host-only, InputState is written via RPC) ──
		if ( Networking.IsHost && Vehicle.InputState.headlightsToggled )
		{
			CycleHeadlights();
			_autoHeadlights = false; // Player took manual control
		}

		if ( Networking.IsHost && Vehicle.InputState.hazardLightsPressed )
		{
			TurnSignal = TurnSignal == TurnSignalState.Hazard
				? TurnSignalState.Off
				: TurnSignalState.Hazard;
			ResetBlink();
		}
		else if ( Networking.IsHost && Vehicle.InputState.turnSignalLeftPressed )
		{
			TurnSignal = TurnSignal == TurnSignalState.Left
				? TurnSignalState.Off
				: TurnSignalState.Left;
			ResetBlink();
		}
		else if ( Networking.IsHost && Vehicle.InputState.turnSignalRightPressed )
		{
			TurnSignal = TurnSignal == TurnSignalState.Right
				? TurnSignalState.Off
				: TurnSignalState.Right;
			ResetBlink();
		}

		// ── Auto headlights (day/night cycle) ────────────────────────
		if ( Networking.IsHost && _dayNight.IsValid() )
		{
			bool hasDriver = Vehicle.Seats.Any( s => s.HasInput && s.Player.IsValid() );
			bool isNight = !_dayNight.IsDay;

			if ( hasDriver && isNight && Headlights == HeadlightState.Off )
			{
				Headlights = HeadlightState.LowBeam;
				_autoHeadlights = true;
			}
			else if ( _autoHeadlights && (!hasDriver || !isNight) )
			{
				Headlights = HeadlightState.Off;
				_autoHeadlights = false;
			}
		}

		// ── Blink timer ──────────────────────────────────────────────
		if ( TurnSignal != TurnSignalState.Off )
		{
			if ( _blinkTimer > BlinkInterval )
			{
				_blinkOn = !_blinkOn;
				_blinkTimer = 0;
			}
		}
		else
		{
			_blinkOn = false;
		}

		UpdateLights();
	}

	private void ResetBlink()
	{
		_blinkTimer = 0;
		_blinkOn = true;
	}

	private void CycleHeadlights()
	{
		Headlights = Headlights switch
		{
			HeadlightState.Off => HeadlightState.LowBeam,
			HeadlightState.LowBeam => HeadlightState.HighBeam,
			HeadlightState.HighBeam => HeadlightState.Off,
			_ => HeadlightState.Off
		};
	}

	private void UpdateLights()
	{
		bool isBraking = Vehicle.DbgBrakeInput > 0.1f || Vehicle.IsHandbrakeActive;
		bool isReverse = Vehicle.Gearbox.IsValid() && Vehicle.Gearbox.IsReverse;
		bool hasDriver = Vehicle.Seats.Any( s => s.HasInput && s.Player.IsValid() );

		// ── Front lights ─────────────────────────────────────────────
		bool lowBeamOn = Headlights == HeadlightState.LowBeam || Headlights == HeadlightState.HighBeam;
		bool highBeamOn = Headlights == HeadlightState.HighBeam;

		SetAll( LowBeams, lowBeamOn );
		SetAll( HighBeams, highBeamOn );

		// ── Rear lights ──────────────────────────────────────────────
		bool brakeLightOn = hasDriver && isBraking && !isReverse;
		SetAll( BrakeLights, brakeLightOn );
		SetAll( ReverseLights, hasDriver && isReverse );

		// ── Turn signals ─────────────────────────────────────────────
		bool leftOn = _blinkOn && (TurnSignal == TurnSignalState.Left || TurnSignal == TurnSignalState.Hazard);
		bool rightOn = _blinkOn && (TurnSignal == TurnSignalState.Right || TurnSignal == TurnSignalState.Hazard);

		SetAll( TurnSignalLeft, leftOn );
		SetAll( TurnSignalRight, rightOn );
	}

	private void SetAll( List<GameObject> objects, bool enabled )
	{
		if ( objects == null ) return;
		foreach ( var obj in objects )
		{
			if ( obj.IsValid() )
				obj.Enabled = enabled;
		}
	}
}
