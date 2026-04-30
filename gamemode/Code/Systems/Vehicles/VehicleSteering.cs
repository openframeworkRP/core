using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Ported from lodzero's SteeringModel.
/// Speed-dependent steering with Curve-based angle limit and Approach smoothing.
/// Sets SteerAngle on each Wheel component instead of rotating GameObjects.
/// </summary>
public sealed class Steering : Component
{
	/// <summary>Maximum steering angle in degrees at standstill.</summary>
	[Property] public float MaxSteerAngle { get; set; } = 25f;

	/// <summary>How fast the steering moves toward target (degrees/sec factor).</summary>
	[Property] public float SteeringSpeed { get; set; } = 35.0f;

	/// <summary>Multiplier on SteeringSpeed when the player releases steering input (auto-center).</summary>
	[Property] public float SteerResetMultiplier { get; set; } = 2.0f;

	/// <summary>Speed (km/h) at which the steering curve starts reducing lock.</summary>
	[Property] public float SteeringMinSpeed { get; set; } = 0.0f;

	/// <summary>Speed (km/h) at which the steering curve reaches its maximum reduction.</summary>
	[Property] public float SteeringMaxSpeed { get; set; } = 140.0f;

	/// <summary>
	/// Curve that maps normalized speed (0..1) to available steering fraction (0..1).
	/// At 0 speed → typically 1.0 (full lock). At high speed → lower (e.g. 0.3).
	/// </summary>
	[Property] public Curve SteeringCurve { get; set; }

	/// <summary>Current steering angle in degrees (synced so passengers see wheel turn).</summary>
	[Sync( SyncFlags.FromHost )] public float CurrentAngle { get; private set; }

	private Vehicle _vehicle;
	private Rigidbody _rb;

	protected override void OnEnabled()
	{
		_vehicle = Components.GetInAncestorsOrSelf<Vehicle>();
		_rb = Components.GetInAncestorsOrSelf<Rigidbody>();
	}

	protected override void OnFixedUpdate()
	{
		if ( Scene.IsEditor || !_vehicle.IsValid() )
			return;

		// Non-host clients: apply synced steer angle to wheels for visuals, then return
		if ( !Networking.IsHost )
		{
			foreach ( var wheel in _vehicle.AllWheels() )
			{
				if ( wheel.SteeringRatio > 0f )
					wheel.SteerAngle = CurrentAngle;
			}
			return;
		}

		var inputState = _vehicle.InputState;
		float steerInput = inputState.direction.y; // -1 left, +1 right

		// ── Speed-dependent steering lock (lodzero's SteeringModel) ──────
		float speed = 0f;
		if ( _rb.IsValid() )
			speed = _rb.Velocity.WithZ( 0f ).Length.InchToMeter() * 3.6f; // m/s → km/h

		float normalizedSpeed = speed.Remap( SteeringMinSpeed, SteeringMaxSpeed, 0f, 1f ).Clamp( 0f, 1f );
		float curveFactor = (SteeringCurve.Frames != null && SteeringCurve.Frames.Length > 0)
			? SteeringCurve.Evaluate( normalizedSpeed )
			: 1.0f; // No curve = full lock at all speeds
		float targetLock = curveFactor * MaxSteerAngle;

		// Map input (-1..1) to angle range
		float targetAngle = steerInput.Remap( -1f, 1f, -targetLock, targetLock );

		// ── Approach smoothing (lodzero style) ───────────────────────────
		float steerSpeed = SteeringSpeed * ((steerInput == 0f) ? SteerResetMultiplier : 1.0f);

		// Speed up steering near full lock (feels more responsive)
		steerSpeed *= MathF.Abs( CurrentAngle ).Remap( 0f, MaxSteerAngle, 1f, 1.5f );

		CurrentAngle = CurrentAngle.Approach( targetAngle, Time.Delta * steerSpeed );

		// ── Apply SteerAngle to all wheels ───────────────────────────────
		foreach ( var wheel in _vehicle.AllWheels() )
		{
			// Only steer wheels with SteeringRatio > 0
			if ( wheel.SteeringRatio > 0f )
				wheel.SteerAngle = CurrentAngle;
		}
	}
}
