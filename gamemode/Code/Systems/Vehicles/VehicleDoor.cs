namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// A vehicle door that opens/closes with rotation animation.
/// Place this component on the door GameObject (the pivot point).
/// Multiple seats can share the same door (e.g. 2 doors, 5 seats).
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Door" )]
[Icon( "door_front" )]
public sealed class VehicleDoor : Component
{
	/// <summary>Open rotation relative to the closed position (typically yaw only, e.g. 0,0,70).</summary>
	[Property] public Angles OpenAngles { get; set; } = new( 0f, 70f, 0f );

	/// <summary>How fast the door opens/closes (degrees per second).</summary>
	[Property] public float Speed { get; set; } = 5f;

	/// <summary>Seconds the door stays open before auto-closing.</summary>
	[Property] public float AutoCloseDelay { get; set; } = 1.5f;

	/// <summary>Is the door currently open or opening?</summary>
	[Sync( SyncFlags.FromHost )] public bool IsOpen { get; private set; }

	private Rotation _closedRotation;
	private Rotation _openRotation;
	private TimeSince _timeSinceOpened;
	private bool _initialized;

	protected override void OnEnabled()
	{
		_closedRotation = LocalRotation;
		_openRotation = _closedRotation * Rotation.From( OpenAngles );
		_initialized = true;
	}

	protected override void OnUpdate()
	{
		if ( !_initialized ) return;

		// Auto-close after delay
		if ( IsOpen && _timeSinceOpened > AutoCloseDelay )
		{
			Close();
		}

		// Smooth rotation toward target
		var target = IsOpen ? _openRotation : _closedRotation;
		LocalRotation = Rotation.Lerp( LocalRotation, target, Time.Delta * Speed );
	}

	/// <summary>Open the door and reset the auto-close timer.</summary>
	public void Open()
	{
		IsOpen = true;
		_timeSinceOpened = 0f;
	}

	/// <summary>Close the door.</summary>
	public void Close()
	{
		IsOpen = false;
	}
}
