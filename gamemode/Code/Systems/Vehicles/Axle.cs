namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Groups a left and right wheel into an axle.
/// Handles stepping both wheels and applying anti-roll bar forces
/// to reduce body roll in corners.
/// </summary>
[Category( "Vehicles" )]
[Title( "Axle" )]
[Icon( "swap_horiz" )]
public sealed class Axle : Component
{
	[Property] public Wheel Left { get; set; }
	[Property] public Wheel Right { get; set; }

	/// <summary>Anti-roll bar stiffness in N/m. Higher = less body roll.</summary>
	[Property] public float AntiRollStiffness { get; set; } = 30000f;

	private Rigidbody _rb;

	protected override void OnEnabled()
	{
		_rb = Components.GetInAncestorsOrSelf<Rigidbody>();
	}

	/// <summary>
	/// Steps both wheels and applies anti-roll forces.
	/// Called by Vehicle.OnFixedUpdate each physics tick.
	/// When visualOnly is true (client-side), only computes wheel state for rendering — no forces applied.
	/// </summary>
	public void Step( bool visualOnly = false )
	{
		if ( Left.IsValid() ) Left.Step( visualOnly );
		if ( Right.IsValid() ) Right.Step( visualOnly );

		if ( !visualOnly )
			ApplyAntiRoll();
	}

	private void ApplyAntiRoll()
	{
		if ( !_rb.IsValid() ) return;
		if ( !Left.IsValid() || !Right.IsValid() ) return;

		// Only apply if at least one wheel is on the ground
		if ( Left.Fz <= 0f && Right.Fz <= 0f ) return;

		// Compression difference drives the anti-roll force
		float rollDiff = Left.Compression - Right.Compression;
		float antiRollForce = rollDiff * AntiRollStiffness;

		// Push down the side that's more extended, push up the compressed side
		// Note: Oz applies this directly without inch conversion (force is already calibrated)
		if ( Left.Fz > 0f )
		{
			Vector3 leftForce = Left.ContactNormal * -antiRollForce * 0.5f;
			_rb.ApplyForceAt( Left.ContactPosition, leftForce );
		}

		if ( Right.Fz > 0f )
		{
			Vector3 rightForce = Right.ContactNormal * antiRollForce * 0.5f;
			_rb.ApplyForceAt( Right.ContactPosition, rightForce );
		}
	}
}
