using System;
using Sandbox.Physics;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Vehicles.UI;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Hitch point placed on both the vehicle (tow ball) and the trailer (tongue).
/// Uses a BallSocketJoint so the trailer pivots freely like a real ball hitch.
/// </summary>
[Category( "Vehicles" )]
[Title( "Trailer Hitch" )]
[Icon( "link" )]
public sealed class TrailerHitch : Component
{
	public enum HitchType
	{
		/// <summary>On the vehicle — the tow ball.</summary>
		Vehicle,
		/// <summary>On the trailer — the tongue/coupler.</summary>
		Trailer
	}

	// ── Configuration ─────────────────────────────────────────────────────────

	[Property, Group( "Hitch" )] public HitchType Type { get; set; } = HitchType.Vehicle;

	/// <summary>Maximum distance (inches) at which coupling can occur.</summary>
	[Property, Group( "Hitch" )] public float CoupleDistance { get; set; } = 30f;

	/// <summary>Force required to break the hitch (0 = unbreakable).</summary>
	[Property, Group( "Hitch" )] public float BreakForce { get; set; } = 0f;

	/// <summary>Interaction distance for the radial menu (inches).</summary>
	[Property, Group( "Hitch" )] public float InteractionDistance { get; set; } = 80f;

	// ── State ─────────────────────────────────────────────────────────────────

	/// <summary>The other hitch we're connected to (synced so clients know the connection state).</summary>
	[Sync( SyncFlags.FromHost )]
	public TrailerHitch ConnectedHitch { get; private set; }

	/// <summary>True if currently connected to another hitch.</summary>
	public bool IsConnected => ConnectedHitch.IsValid();

	/// <summary>The vehicle we're connected to (only valid on trailer-type hitches).</summary>
	public Vehicle ConnectedVehicle => ConnectedHitch.IsValid()
		? ConnectedHitch.Components.GetInAncestorsOrSelf<Vehicle>()
		: null;

	/// <summary>The trailer we're connected to (only valid on vehicle-type hitches).</summary>
	public Trailer ConnectedTrailer => ConnectedHitch.IsValid()
		? ConnectedHitch.Components.GetInAncestorsOrSelf<Trailer>()
		: null;

	// ── Internals ─────────────────────────────────────────────────────────────

	private BallSocketJoint _joint;

	// ── Joint break detection ─────────────────────────────────────────────────

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !IsConnected ) return;
		if ( Type != HitchType.Vehicle ) return;

		// If the joint was destroyed by the physics engine (break force exceeded), decouple
		if ( _joint == null || _joint.Body1 == null || _joint.Body2 == null )
		{
			_joint = null;
			var other = ConnectedHitch;
			ConnectedHitch = null;
			if ( other.IsValid() )
			{
				other._joint = null;
				other.ConnectedHitch = null;
			}
			Log.Info( "[TrailerHitch] Joint broke — decoupled." );
		}
	}

	// ── Radial Menu Detection ─────────────────────────────────────────────────

	protected override void OnUpdate()
	{
		// Skip on dedicated server (no local player to show UI to)
		if ( !Networking.IsClient ) return;
		if ( !Input.Pressed( "Use" ) ) return;

		var pawn = Game.ActiveScene?.GetAllComponents<PlayerPawn>()
			.FirstOrDefault( x => !x.IsProxy );
		if ( pawn == null ) return;

		// Don't open if player is in a vehicle
		if ( pawn.CurrentCar.IsValid() ) return;

		float dist = Vector3.DistanceBetween( pawn.WorldPosition, WorldPosition );
		if ( dist > InteractionDistance ) return;

		TrailerRadialMenu.Open( this );
	}

	// ── RPC methods (called from radial menu) ─────────────────────────────────

	[Rpc.Host]
	public void RequestCouple()
	{
		TryCouple();
	}

	[Rpc.Host]
	public void RequestDecouple()
	{
		Decouple();
	}

	/// <summary>
	/// Attempts to couple this hitch with the nearest compatible hitch.
	/// </summary>
	public bool TryCouple()
	{
		if ( IsConnected ) return false;

		var targetType = Type == HitchType.Vehicle ? HitchType.Trailer : HitchType.Vehicle;

		TrailerHitch best = null;
		float bestDist = CoupleDistance;

		foreach ( var hitch in Scene.GetAll<TrailerHitch>() )
		{
			if ( hitch == this ) continue;
			if ( hitch.Type != targetType ) continue;
			if ( hitch.IsConnected ) continue;

			float dist = Vector3.DistanceBetween( WorldPosition, hitch.WorldPosition );
			if ( dist < bestDist )
			{
				bestDist = dist;
				best = hitch;
			}
		}

		if ( best == null ) return false;

		Couple( best );
		return true;
	}

	/// <summary>
	/// Couples this hitch to another hitch using a BallSocketJoint.
	/// </summary>
	public void Couple( TrailerHitch other )
	{
		if ( !Networking.IsHost ) return;
		if ( IsConnected || other.IsConnected ) return;

		var vehicleHitch = Type == HitchType.Vehicle ? this : other;
		var trailerHitch = Type == HitchType.Trailer ? this : other;

		var vehicleRoot = vehicleHitch.GameObject.Root;
		var trailerRoot = trailerHitch.GameObject.Root;

		var vehicleRb = vehicleRoot.Components.Get<Rigidbody>();
		var trailerRb = trailerRoot.Components.Get<Rigidbody>();

		if ( !vehicleRb.IsValid() || vehicleRb.PhysicsBody is null ) return;
		if ( !trailerRb.IsValid() || trailerRb.PhysicsBody is null ) return;

		// Snap trailer so hitch points align before creating the joint
		var offset = vehicleHitch.WorldPosition - trailerHitch.WorldPosition;
		trailerRoot.WorldPosition += offset;

		// Create ball socket joint at the hitch point
		// PhysicsPoint.Local — the hitch position in each body's local space
		var vehLocalPos = vehicleRb.PhysicsBody.Transform.PointToLocal( vehicleHitch.WorldPosition );
		var trlLocalPos = trailerRb.PhysicsBody.Transform.PointToLocal( trailerHitch.WorldPosition );

		var pointA = PhysicsPoint.Local( vehicleRb.PhysicsBody, vehLocalPos, null );
		var pointB = PhysicsPoint.Local( trailerRb.PhysicsBody, trlLocalPos, null );

		_joint = PhysicsJoint.CreateBallSocket( pointA, pointB );
		_joint.SwingLimitEnabled = false;
		_joint.TwistLimitEnabled = false;

		if ( BreakForce > 0f )
			_joint.Strength = BreakForce;

		// Link both hitches
		ConnectedHitch = other;
		other.ConnectedHitch = this;
		other._joint = _joint;

		Log.Info( $"[TrailerHitch] Coupled: {vehicleRoot.Name} <-> {trailerRoot.Name}" );
	}

	/// <summary>
	/// Decouples the hitch.
	/// </summary>
	public void Decouple()
	{
		if ( !Networking.IsHost ) return;
		if ( !IsConnected ) return;

		var other = ConnectedHitch;

		// Remove physics joint
		_joint?.Remove();
		_joint = null;

		if ( other.IsValid() )
		{
			other._joint = null;
			other.ConnectedHitch = null;
		}

		ConnectedHitch = null;

		Log.Info( "[TrailerHitch] Decoupled." );
	}

	// ── Cleanup ───────────────────────────────────────────────────────────────

	protected override void OnDestroy()
	{
		if ( IsConnected )
			Decouple();
	}

	// ── Gizmos ────────────────────────────────────────────────────────────────

	protected override void DrawGizmos()
	{
		var color = Type == HitchType.Vehicle ? Color.Green : Color.Orange;
		Gizmo.Draw.Color = color;
		Gizmo.Draw.SolidSphere( Vector3.Zero, 2f );

		Gizmo.Draw.Color = color.WithAlpha( 0.15f );
		Gizmo.Draw.LineSphere( Vector3.Zero, CoupleDistance );

		Gizmo.Draw.Color = color.WithAlpha( 0.06f );
		Gizmo.Draw.LineSphere( Vector3.Zero, InteractionDistance );

		if ( IsConnected )
		{
			Gizmo.Draw.Color = Color.Yellow;
			var localTarget = WorldTransform.PointToLocal( ConnectedHitch.WorldPosition );
			Gizmo.Draw.Line( Vector3.Zero, localTarget );
		}
	}
}
