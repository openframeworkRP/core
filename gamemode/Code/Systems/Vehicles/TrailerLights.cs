namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Controls trailer lights: brake lights, reverse lights, and turn signals.
/// Mirrors the towing vehicle's light state when attached.
/// All lights turn off when the trailer is detached.
/// </summary>
[Category( "Vehicles" )]
[Title( "Trailer Lights" )]
[Icon( "lightbulb" )]
public sealed class TrailerLights : Component
{
	[Property, Group( "References" )] public Trailer Trailer { get; set; }

	// ── Rear lights ───────────────────────────────────────────────────────────

	/// <summary>Rear brake lights.</summary>
	[Property, Group( "Rear" )] public List<GameObject> BrakeLights { get; set; }

	/// <summary>Rear reverse lights (white).</summary>
	[Property, Group( "Rear" )] public List<GameObject> ReverseLights { get; set; }

	// ── Turn signals ──────────────────────────────────────────────────────────

	/// <summary>Left turn signal lights.</summary>
	[Property, Group( "Turn Signals" )] public List<GameObject> TurnSignalLeft { get; set; }

	/// <summary>Right turn signal lights.</summary>
	[Property, Group( "Turn Signals" )] public List<GameObject> TurnSignalRight { get; set; }

	/// <summary>Blink interval in seconds.</summary>
	[Property, Group( "Turn Signals" )] public float BlinkInterval { get; set; } = 0.5f;

	// ── Internals ─────────────────────────────────────────────────────────────

	private TimeSince _blinkTimer;
	private bool _blinkOn;

	protected override void OnEnabled()
	{
		SetAll( BrakeLights, false );
		SetAll( ReverseLights, false );
		SetAll( TurnSignalLeft, false );
		SetAll( TurnSignalRight, false );
	}

	protected override void OnUpdate()
	{
		if ( !Trailer.IsValid() ) return;

		var vehicle = Trailer.AttachedVehicle;

		// Not attached — all lights off
		if ( !vehicle.IsValid() )
		{
			SetAll( BrakeLights, false );
			SetAll( ReverseLights, false );
			SetAll( TurnSignalLeft, false );
			SetAll( TurnSignalRight, false );
			_blinkOn = false;
			return;
		}

		// Read vehicle light state
		var vehicleLights = vehicle.Components.GetInDescendantsOrSelf<VehicleLights>();

		bool hasDriver = vehicle.Seats.Any( s => s.HasInput && s.Player.IsValid() );
		bool isBraking = vehicle.DbgBrakeInput > 0.1f || vehicle.IsHandbrakeActive;
		bool isReverse = vehicle.Gearbox.IsValid() && vehicle.Gearbox.IsReverse;

		// ── Brake / Reverse ──────────────────────────────────────────
		SetAll( BrakeLights, hasDriver && isBraking && !isReverse );
		SetAll( ReverseLights, hasDriver && isReverse );

		// ── Turn signals (mirror vehicle) ────────────────────────────
		var turnState = vehicleLights.IsValid()
			? vehicleLights.TurnSignal
			: VehicleLights.TurnSignalState.Off;

		if ( turnState != VehicleLights.TurnSignalState.Off )
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

		bool leftOn = _blinkOn && (turnState == VehicleLights.TurnSignalState.Left || turnState == VehicleLights.TurnSignalState.Hazard);
		bool rightOn = _blinkOn && (turnState == VehicleLights.TurnSignalState.Right || turnState == VehicleLights.TurnSignalState.Hazard);

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
