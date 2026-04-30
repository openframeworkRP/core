using OpenFramework.Systems.Pawn;

namespace OpenFramework.Systems.Vehicles;

public sealed class PlayerSeat : Component
{
	[Property] public Vehicle Vehicle { get; set; }
	[Property] public bool HasInput { get; set; } = true;

	/// <summary>The door this seat uses to enter/exit. Multiple seats can share one door.</summary>
	[Property] public VehicleDoor Door { get; set; }

	/// <summary>Local position offset for the seated player.</summary>
	[Property] public Vector3 SeatOffset { get; set; } = Vector3.Zero;

	/// <summary>Local rotation for the seated player.</summary>
	[Property] public Angles SeatAngles { get; set; } = Angles.Zero;

	// ── IK Anchor Points (place empty GameObjects in the vehicle prefab) ─────

	/// <summary>Left hand grip on the steering wheel.</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_HandLeft { get; set; }

	/// <summary>Right hand grip on the steering wheel.</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_HandRight { get; set; }

	/// <summary>Right foot rest position (above gas pedal). The foot rotates down when throttle is applied.</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_FootGas { get; set; }

	/// <summary>Right foot brake pedal position.</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_FootBrake { get; set; }

	/// <summary>Left foot resting position (dead pedal / clutch area).</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_FootLeft { get; set; }

	/// <summary>Handbrake grip position. Right hand moves here when handbrake is active.</summary>
	[Property, Group( "IK Anchors" )] public GameObject IK_Handbrake { get; set; }

	/// <summary>Steering wheel pivot (rotates with steering angle for visual effect).</summary>
	[Property, Group( "IK Anchors" )] public GameObject SteeringWheelPivot { get; set; }

	/// <summary>Sit height offset for the citizen animgraph. Adjusts pelvis height in sitting pose.</summary>
	[Property, Group( "IK Anchors" )] public float SitOffsetHeight { get; set; } = -6.85f;

	[Sync( SyncFlags.FromHost )] public PlayerPawn Player { get; private set; }

	/// <summary>Local offset from vehicle where the player entered — used as exit point.</summary>
	private Vector3 _entryLocalOffset;

	private TimeSince _timeSinceChanged;

	// Smooth IK blending
	private Vector3 _smoothRightHandPos;
	private Vector3 _smoothRightFootPos;
	private float _ikBlendIn;

	public bool CanEnter( PlayerPawn player )
	{
		return !Player.IsValid();
	}

	public bool Enter( PlayerPawn player )
	{
		if ( !CanEnter( player ) )
			return false;

		if ( !Networking.IsHost )
			return false;

		if ( Vehicle.IsValid() && Vehicle.ShowDebugLogs )
			Log.Info( $"[Seat] Enter: player={player.GameObject.Name} seat={GameObject.Name}" );

		// Open the door
		Door?.Open();

		// Remember entry offset relative to the vehicle
		_entryLocalOffset = Vehicle.Transform.World.PointToLocal( player.WorldPosition );

		Player = player;
		_timeSinceChanged = 0;

		// Cancel any impulse the physics engine may have applied during the overlap
		if ( Vehicle.IsValid() && Vehicle.Rigidbody.IsValid() )
		{
			Vehicle.Rigidbody.Velocity = Vector3.Zero;
			Vehicle.Rigidbody.AngularVelocity = Vector3.Zero;
		}

		// Broadcast to ALL clients: parent player, disable physics, play sit anim
		BroadcastSeatEnter( player );

		return true;
	}

	[Rpc.Broadcast]
	private void BroadcastSeatEnter( PlayerPawn player )
	{
		if ( !player.IsValid() ) return;

		// Enable the seat component on ALL clients so OnUpdate runs (animation + IK)
		Enabled = true;

		// Tell PlayerPawn we're in a car so its movement code yields
		SetCurrentCar( player, true );

		// Disable player physics BEFORE parenting to avoid collision with vehicle
		SetPlayerPhysics( player, false );

		// Move player to seat world position BEFORE parenting to prevent overlap impulse
		player.WorldPosition = WorldPosition;

		// Parent player to seat
		player.GameObject.SetParent( GameObject );
		player.LocalPosition = SeatOffset;
		player.LocalRotation = Rotation.From( SeatAngles );

		// Reset IK blend
		_ikBlendIn = 0f;
	}

	public bool CanLeave( PlayerPawn player )
	{
		if ( !Player.IsValid() ) return false;
		if ( _timeSinceChanged < 0.5f ) return false;
		if ( Player != player ) return false;
		return true;
	}

	public bool Leave( PlayerPawn player )
	{
		if ( !CanLeave( player ) )
			return false;

		if ( !Networking.IsHost )
			return false;

		// Open the door for exit
		Door?.Open();

		var leavingPlayer = Player;
		Player = null;

		// Libère tout storage véhicule ouvert par ce joueur
		VehicleStorage.Release( leavingPlayer.Client );

		var exitPos = FindExitLocation();
		var exitVelocity = (Vehicle.IsValid() && Vehicle.Rigidbody.IsValid())
			? Vehicle.Rigidbody.Velocity
			: Vector3.Zero;

		// Broadcast to ALL clients: unparent player, re-enable physics, restore anim
		BroadcastSeatLeave( leavingPlayer, exitPos, exitVelocity );

		return true;
	}

	[Rpc.Broadcast]
	private void BroadcastSeatLeave( PlayerPawn leavingPlayer, Vector3 exitPos, Vector3 exitVelocity )
	{
		if ( !leavingPlayer.IsValid() ) return;

		// Disable the seat component on ALL clients
		Enabled = false;

		// Clear car reference so PlayerPawn movement resumes
		SetCurrentCar( leavingPlayer, false );

		// Unparent player
		leavingPlayer.GameObject.SetParent( null );

		// Move player to exit point
		leavingPlayer.WorldPosition = exitPos;

		// Re-enable player physics
		SetPlayerPhysics( leavingPlayer, true );

		// Give the player the vehicle's velocity so they don't stop dead
		if ( leavingPlayer.CharacterController.IsValid() )
			leavingPlayer.CharacterController.Velocity = exitVelocity;

		// Re-enable AnimationHelpers + restore standing animation + disable IK
		if ( leavingPlayer.Body.IsValid() )
		{
			foreach ( var helper in leavingPlayer.Body.AnimationHelpers )
			{
				if ( helper.IsValid() )
				{
					helper.Enabled = true;
					helper.IsSitting = false;
				}
			}

			if ( leavingPlayer.BodyRenderer.IsValid() )
			{
				var r = leavingPlayer.BodyRenderer;
				r.Set( "b_sit", false );
				r.Set( "sit", 0 );
				r.Set( "ik.hand_left.enabled", false );
				r.Set( "ik.hand_right.enabled", false );
				r.Set( "ik.foot_left.enabled", false );
				r.Set( "ik.foot_right.enabled", false );
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( !Player.IsValid() )
			return;

		// Fallback: if the broadcast hasn't run yet, force parenting on this client
		if ( Player.GameObject.Parent != GameObject )
		{
			SetPlayerPhysics( Player, false );
			Player.GameObject.SetParent( GameObject );
			Player.LocalPosition = SeatOffset;
			Player.LocalRotation = Rotation.From( SeatAngles );
		}

		if ( !Player.Body.IsValid() )
			return;

		// Keep the body facing forward relative to the seat
		Player.Body.UpdateRotation( WorldRotation * Rotation.From( SeatAngles ) );

		// Disable AnimationHelpers so they don't override our IK
		foreach ( var helper in Player.Body.AnimationHelpers )
		{
			if ( helper.IsValid() )
				helper.Enabled = false;
		}

		// Set all anim params directly on the renderer (no AnimationHelper)
		if ( Player.BodyRenderer.IsValid() )
		{
			var r = Player.BodyRenderer;
			r.Set( "b_sit", true );
			r.Set( "sit", 1 );
			r.Set( "sit_pose", 1 );
			r.Set( "sit_offset_height", SitOffsetHeight );
			r.Set( "b_grounded", true );
			r.Set( "move_speed", 0f );
			r.Set( "move_groundspeed", 0f );
			r.Set( "wish_speed", 0f );
			r.Set( "wish_groundspeed", 0f );
			r.Set( "holdtype", 0 );

			// ── Head look direction (synced via EyeAngles) ──────────────
			var lookDir = Player.EyeAngles.Forward;
			var localLook = r.WorldRotation.Inverse * lookDir;
			var lookAngles = Rotation.LookAt( localLook ).Angles();
			r.Set( "aim_body_pitch", lookAngles.pitch );
			r.Set( "aim_body_yaw", lookAngles.yaw );
			r.Set( "aim_body_weight", 0.5f );

			// ── IK for hands and feet ────────────────────────────────────
			UpdateIK( r );
		}
	}

	private TimeSince _ikDebugLog;

	private void UpdateIK( SkinnedModelRenderer r )
	{
		float dt = Time.Delta;

		// Smooth blend in IK over 0.5s after entering
		_ikBlendIn = MathX.Lerp( _ikBlendIn, 1f, dt * 4f );

		// Get vehicle input state for animations
		var input = Vehicle?.InputState;
		float steerAngle = Vehicle?.Components.Get<Steering>()?.CurrentAngle ?? 0f;
		bool isBraking = input != null && input.direction.x < -0.1f;
		bool isHandbraking = input?.isHandbraking ?? false;

		// Debug log every 2s (only when Vehicle.ShowDebugLogs is on)
		if ( Vehicle.IsValid() && Vehicle.ShowDebugLogs && _ikDebugLog > 2f )
		{
			_ikDebugLog = 0;
			Log.Info( $"[IK:Debug] Anchors: HandL={IK_HandLeft.IsValid()} HandR={IK_HandRight.IsValid()} FootGas={IK_FootGas.IsValid()}" );
			Log.Info( $"[IK:Debug] SteerAngle={steerAngle:F1} Braking={isBraking} Handbraking={isHandbraking}" );
			if ( IK_HandLeft.IsValid() )
			{
				var localTx = r.Transform.World.ToLocal( IK_HandLeft.Transform.World );
				Log.Info( $"[IK:Debug] HandL WorldPos={IK_HandLeft.WorldPosition} LocalPos={localTx.Position}" );
			}

			Log.Info( $"[IK:Debug] ReadBack: hand_left.enabled={r.GetBool( "ik.hand_left.enabled" )}" );
			Log.Info( $"[IK:Debug] ReadBack: foot_right.enabled={r.GetBool( "ik.foot_right.enabled" )}" );
			Log.Info( $"[IK:Debug] BodyRenderer WorldPos={r.WorldPosition}" );
			Log.Info( $"[IK:Debug] AnimHelpers disabled: {Player.Body.AnimationHelpers.All( h => !h.IsValid() || !h.Enabled )}" );
		}

		// ── Rotate steering wheel pivot ──────────────────────────────
		if ( SteeringWheelPivot.IsValid() )
		{
			// Steering wheel rotates around its local Z (roll) axis
			SteeringWheelPivot.LocalRotation = Rotation.FromRoll( -steerAngle * 4f );
		}

		// ── Left Hand → steering wheel ───────────────────────────────
		if ( IK_HandLeft.IsValid() )
		{
			SetIkTarget( r, "hand_left", IK_HandLeft.WorldPosition, IK_HandLeft.WorldRotation );
		}

		// ── Right Hand → steering wheel or handbrake ─────────────────
		if ( IK_HandRight.IsValid() )
		{
			Vector3 targetRightHand = IK_HandRight.WorldPosition;
			Rotation targetRightHandRot = IK_HandRight.WorldRotation;

			// If handbrake is pulled, move right hand to handbrake
			if ( isHandbraking && IK_Handbrake.IsValid() )
			{
				targetRightHand = IK_Handbrake.WorldPosition;
				targetRightHandRot = IK_Handbrake.WorldRotation;
			}

			_smoothRightHandPos = Vector3.Lerp( _smoothRightHandPos, targetRightHand, dt * 8f );

			SetIkTarget( r, "hand_right", _smoothRightHandPos, targetRightHandRot );
		}

		// ── Left Foot → dead pedal / clutch ──────────────────────────
		if ( IK_FootLeft.IsValid() )
		{
			SetIkTarget( r, "foot_left", IK_FootLeft.WorldPosition, IK_FootLeft.WorldRotation );
		}

		// ── Right Foot → gas pedal or brake pedal ────────────────────
		if ( IK_FootGas.IsValid() )
		{
			Vector3 targetFoot = IK_FootGas.WorldPosition;
			Rotation targetFootRot = IK_FootGas.WorldRotation;

			if ( isBraking && IK_FootBrake.IsValid() )
			{
				targetFoot = IK_FootBrake.WorldPosition;
				targetFootRot = IK_FootBrake.WorldRotation;
			}

			_smoothRightFootPos = Vector3.Lerp( _smoothRightFootPos, targetFoot, dt * 10f );
			SetIkTarget( r, "foot_right", _smoothRightFootPos, targetFootRot );
		}
	}

	/// <summary>Sets an IK target, converting world position/rotation to model-local space.</summary>
	private void SetIkTarget( SkinnedModelRenderer r, string ikName, Vector3 worldPos, Rotation worldRot )
	{
		var localTx = r.Transform.World.ToLocal( new Transform( worldPos, worldRot ) );
		r.Set( $"ik.{ikName}.enabled", true );
		r.Set( $"ik.{ikName}.position", localTx.Position );
		r.Set( $"ik.{ikName}.rotation", localTx.Rotation );
	}

	internal void Eject()
	{
		if ( Player.IsValid() )
			Leave( Player );
	}

	private void SetCurrentCar( PlayerPawn player, bool inVehicle )
	{
		player.CurrentCar = inVehicle ? Vehicle : null;
	}

	private void SetPlayerPhysics( PlayerPawn player, bool enabled )
	{
		if ( player.CharacterController.IsValid() )
		{
			player.CharacterController.Velocity = Vector3.Zero;
			player.CharacterController.Enabled = enabled;
		}

		foreach ( var col in player.Components.GetAll<Collider>() )
		{
			if ( col.IsValid() )
				col.Enabled = enabled;
		}

		var rb = player.Components.Get<Rigidbody>();
		if ( rb.IsValid() ) rb.MotionEnabled = enabled;
	}

	private Vector3 FindExitLocation()
	{
		var exitPos = Vehicle.Transform.World.PointToWorld( _entryLocalOffset );

		var playerSize = new Vector3( 64, 64, 72 );
		var testTrace = Scene.Trace.Box( new BBox( -playerSize / 2, playerSize / 2 ), exitPos, exitPos )
			.WithoutTags( "player", "wheel" )
			.Run();

		if ( !testTrace.Hit )
			return exitPos;

		return FindFreeSpaceNear( exitPos );
	}

	private Vector3 FindFreeSpaceNear( Vector3 position )
	{
		var playerSize = new Vector3( 64, 64, 72 );
		var playerBox = new BBox( -playerSize / 2, playerSize / 2 );
		float step = 16f;
		float maxDistance = 200f;

		for ( float distance = step; distance <= maxDistance; distance += step )
		{
			for ( float x = -distance; x <= distance; x += step )
			{
				for ( float y = -distance; y <= distance; y += step )
				{
					if ( MathF.Abs( x ) != distance && MathF.Abs( y ) != distance )
						continue;

					var testPos = position + new Vector3( x, y, 0 );
					var trace = Scene.Trace.Box( playerBox, testPos, testPos )
						.WithoutTags( "player", "wheel" )
						.Run();

					if ( !trace.Hit )
						return testPos;
				}
			}
		}

		return position;
	}
}
