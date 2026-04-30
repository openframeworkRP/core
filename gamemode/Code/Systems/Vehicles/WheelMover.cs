namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Moves and rotates a visual wheel model to match the physics Wheel state.
/// Positions at the wheel contact center and spins based on angular velocity.
/// </summary>
public sealed class WheelMover : Component
{
	[Property] public Wheel Target { get; set; }
	[Property] public bool ReverseRotation { get; set; }

	protected override void OnFixedUpdate()
	{
		if ( !Target.IsValid() )
			return;

		// Position the visual at the wheel center (above contact point by radius)
		WorldPosition = Target.GetCenter();

		// Apply steering angle (yaw) + spin (roll) based on angular velocity
		float steerAngle = Target.SteerAngle * Target.SteeringRatio;
		float rollDelta = MathX.RadianToDegree( Target.AngularVelocity ) * Time.Delta;
		if ( ReverseRotation ) rollDelta = -rollDelta;

		// Build rotation: first apply steering yaw, then accumulate spin
		var steerRot = Rotation.From( 0f, steerAngle, 0f );
		LocalRotation = steerRot * Rotation.From( 0, 0, _spinAngle );
		_spinAngle += rollDelta;
	}

	private float _spinAngle;
}
