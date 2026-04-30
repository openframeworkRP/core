namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Main trailer component. Manages non-motorized axles and the hitch attachment point.
/// The trailer uses the same Wheel (DrivingRatio=0) and Axle components as vehicles.
/// Physics are handled by a Rigidbody on the same GameObject.
/// </summary>
[Category( "Vehicles" )]
[Title( "Trailer" )]
[Icon( "rv_hookup" )]
public sealed class Trailer : Component
{
	// ── References ────────────────────────────────────────────────────────────

	[Property, Group( "Components" )] public Rigidbody Rigidbody { get; set; }
	[Property, Group( "Components" )] public ModelRenderer Model { get; set; }

	/// <summary>List of axles on this trailer (Wheel with DrivingRatio=0).</summary>
	[Property, Group( "Trailer" )] public List<Axle> Axles { get; set; }

	// ── Physics ───────────────────────────────────────────────────────────────

	/// <summary>Trailer mass in kg (unloaded).</summary>
	[Property, Group( "Physics" )] public float Mass { get; set; } = 500f;

	/// <summary>Linear drag applied to the rigidbody.</summary>
	[Property, Group( "Physics" )] public float LinearDrag { get; set; } = 0.05f;

	/// <summary>Angular drag applied to the rigidbody.</summary>
	[Property, Group( "Physics" )] public float AngularDrag { get; set; } = 0.5f;

	/// <summary>Custom gravity multiplier for the trailer.</summary>
	[Property, Group( "Physics" )] public float GravityScale { get; set; } = 1.0f;

	// ── Hitch Point ──────────────────────────────────────────────────────────

	/// <summary>The TrailerHitch component on this trailer (where it connects to a vehicle).</summary>
	[Property, Group( "Hitch" )] public TrailerHitch Hitch { get; set; }

	// ── State ─────────────────────────────────────────────────────────────────

	/// <summary>True if this trailer is currently attached to a vehicle.</summary>
	public bool IsAttached => Hitch.IsValid() && Hitch.IsConnected;

	/// <summary>The vehicle this trailer is attached to, if any.</summary>
	public Vehicle AttachedVehicle => Hitch.IsValid() ? Hitch.ConnectedVehicle : null;

	protected override void OnStart()
	{
		if ( !Rigidbody.IsValid() )
		{
			Rigidbody = Components.GetInAncestorsOrSelf<Rigidbody>();
		}

		if ( !Rigidbody.IsValid() || Rigidbody.PhysicsBody is null )
			return;

		// Configure physics on host only — host is authoritative over rigidbody state
		if ( Networking.IsHost )
		{
			Rigidbody.MassOverride = Mass;
			Rigidbody.LinearDamping = LinearDrag;
			Rigidbody.AngularDamping = AngularDrag;
			Rigidbody.Gravity = true;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !Rigidbody.IsValid() ) return;

		// Non-host clients: visual wheel updates only (no physics forces)
		if ( !Networking.IsHost )
		{
			StepWheels( visualOnly: true );
			return;
		}

		// Re-apply mass each tick in case PhysicsBody was recreated
		if ( Rigidbody.PhysicsBody is not null )
		{
			Rigidbody.MassOverride = Mass;
		}

		// Host: full physics simulation
		StepWheels( visualOnly: false );
		ApplyGravity();
	}

	private void StepWheels( bool visualOnly )
	{
		if ( Axles == null ) return;

		foreach ( var axle in Axles )
		{
			if ( !axle.IsValid() ) continue;
			axle.Step( visualOnly );
		}
	}

	private void ApplyGravity()
	{
		if ( GravityScale <= 1f ) return;

		float extra = (GravityScale - 1f) * Scene.PhysicsWorld.Gravity.Length * Rigidbody.PhysicsBody.Mass;
		Rigidbody.ApplyForce( Vector3.Down * extra );
	}
}
