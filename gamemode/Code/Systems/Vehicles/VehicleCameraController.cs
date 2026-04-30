using OpenFramework.Systems.Pawn;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Vehicle camera with two modes:
/// - Third person: GTA V style follow camera with free look
/// - First person: head bone tracking with Neck FX (Assetto Corsa style)
/// Toggle with C key. Player camera stays disabled in both modes.
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Camera" )]
[Icon( "videocam" )]
public sealed class VehicleCameraController : Component
{
	[Property] public Vehicle Vehicle { get; set; }

	// ── Third Person (GTA V style) ──────────────────────────────────────────

	/// <summary>How fast the camera yaw auto-centers behind the vehicle.</summary>
	[Property, Group( "Third Person" )] public float AutoCenterSpeed { get; set; } = 3.0f;

	/// <summary>Delay in seconds before auto-center kicks in after mouse movement.</summary>
	[Property, Group( "Third Person" )] public float AutoCenterDelay { get; set; } = 1.5f;

	/// <summary>Mouse sensitivity for free look.</summary>
	[Property, Group( "Third Person" )] public float LookSensitivity { get; set; } = 0.15f;

	/// <summary>Default pitch angle (degrees). Positive = looking down.</summary>
	[Property, Group( "Third Person" )] public float DefaultPitch { get; set; } = 12f;

	/// <summary>Min pitch angle (looking up).</summary>
	[Property, Group( "Third Person" )] public float MinPitch { get; set; } = -20f;

	/// <summary>Max pitch angle (looking down).</summary>
	[Property, Group( "Third Person" )] public float MaxTPPitch { get; set; } = 60f;

	/// <summary>Height offset above the vehicle (inches).</summary>
	[Property, Group( "Third Person" )] public float HeightOffset { get; set; } = 60f;

	/// <summary>Base distance behind the vehicle (inches).</summary>
	[Property, Group( "Third Person" )] public float BaseDistance { get; set; } = 300f;

	/// <summary>Max distance at top speed (inches).</summary>
	[Property, Group( "Third Person" )] public float MaxDistance { get; set; } = 400f;

	// ── Neck FX (first person only) ──────────────────────────────────────────

	/// <summary>Lateral head offset from g-force (inches per g).</summary>
	[Property, Group( "Neck FX" )] public float LateralGain { get; set; } = 4f;

	/// <summary>Longitudinal head offset from g-force (inches per g).</summary>
	[Property, Group( "Neck FX" )] public float LongitudinalGain { get; set; } = 3f;

	/// <summary>Smoothing speed for g-force reactions.</summary>
	[Property, Group( "Neck FX" )] public float GForceSmoothing { get; set; } = 8f;

	/// <summary>Max lateral head offset (inches).</summary>
	[Property, Group( "Neck FX" )] public float MaxLateralOffset { get; set; } = 6f;

	/// <summary>Max longitudinal head offset (inches).</summary>
	[Property, Group( "Neck FX" )] public float MaxLongitudinalOffset { get; set; } = 4f;

	/// <summary>Head roll from lateral g (degrees per g).</summary>
	[Property, Group( "Neck FX" )] public float RollGain { get; set; } = 3f;

	/// <summary>Max head roll (degrees).</summary>
	[Property, Group( "Neck FX" )] public float MaxRoll { get; set; } = 5f;

	/// <summary>Head pitch from longitudinal g (degrees per g). Positive = look up on braking.</summary>
	[Property, Group( "Neck FX" )] public float PitchGain { get; set; } = 2f;

	/// <summary>Max head pitch offset (degrees).</summary>
	[Property, Group( "Neck FX" )] public float MaxPitch { get; set; } = 4f;

	/// <summary>Look-ahead yaw from steering (degrees per degree of steer angle).</summary>
	[Property, Group( "Neck FX" )] public float LookAheadGain { get; set; } = 0.3f;

	// ── Shared ───────────────────────────────────────────────────────────────

	/// <summary>Base field of view.</summary>
	[Property, Group( "Shared" )] public float BaseFOV { get; set; } = 90f;

	/// <summary>Extra FOV at top speed.</summary>
	[Property, Group( "Shared" )] public float SpeedFOVBonus { get; set; } = 8f;

	// ── State ────────────────────────────────────────────────────────────────

	private bool _firstPerson;
	private float _distance;
	private float _currentFOV;
	private CameraComponent _camera;
	private AudioListener _audioListener;
	private bool _active;
	private Rotation _camRotation;
	private Vector3 _prevVelocity;
	private Vector3 _smoothAccel;
	private Vector2 _smoothGOffset;
	private float _smoothRoll;
	private float _smoothPitch;

	// GTA V style free look state
	private float _orbitYaw;
	private float _orbitPitch;
	private float _timeSinceMouseMove;

	// First person mouse look
	private Angles _fpEyeAngles;

	// Mouse delta accumulated in OnUpdate, consumed in OnPreRender
	private Vector2 _pendingMouseDelta;

	private CameraController _playerCamera;
	private CameraMode _previousCameraMode;
	private PlayerPawn _localPlayer;

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() )
			return;

		_localPlayer = GetLocalSeatedPlayer();
		bool shouldBeActive = _localPlayer.IsValid();

		if ( shouldBeActive && !_active )
			Activate( _localPlayer );
		else if ( !shouldBeActive && _active )
			Deactivate();

		if ( !_active || !_camera.IsValid() )
			return;

		// ── Toggle first/third person with C ─────────────────────────
		if ( Input.Keyboard.Pressed( "c" ) )
		{
			_firstPerson = !_firstPerson;
			UpdatePlayerVisibility();
		}

		// ── Accumulate mouse input in OnUpdate (polled here) ─────────
		Vector2 mouseDelta = Input.MouseDelta;
		_pendingMouseDelta += mouseDelta;

		if ( _firstPerson )
			_fpEyeAngles += Input.AnalogLook;
	}

	/// <summary>
	/// Camera positioning runs in OnPreRender for perfect sync with the rendered frame.
	/// This eliminates stuttering caused by physics/render timing mismatch.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( !_active || !_camera.IsValid() )
			return;

		var rb = Vehicle.Rigidbody;
		if ( !rb.IsValid() ) return;

		float dt = Time.Delta;
		if ( dt <= 0f ) return;

		// ── Compute g-forces (smoothed to avoid frame-rate jitter) ──
		Vector3 velocity = rb.Velocity;
		Vector3 rawAccel = (velocity - _prevVelocity) / dt;
		_prevVelocity = velocity;
		_smoothAccel = Vector3.Lerp( _smoothAccel, rawAccel, dt * 10f );
		Vector3 accel = _smoothAccel;

		float speed = velocity.WithZ( 0f ).Length;
		float speedMs = speed.InchToMeter();

		// ── FOV ──────────────────────────────────────────────────────
		float targetFOV = BaseFOV + SpeedFOVBonus * (speedMs / 50f).Clamp( 0f, 1f );
		_currentFOV = MathX.Lerp( _currentFOV, targetFOV, dt * 4f );

		if ( _firstPerson )
		{
			Vector3 localAccel = Vehicle.WorldRotation.Inverse * accel;
			float lateralG = localAccel.y / 386.1f;
			float longitudinalG = localAccel.x / 386.1f;
			UpdateFirstPerson( dt, lateralG, longitudinalG );
		}
		else
		{
			UpdateThirdPerson( dt, speed );
		}

		_camera.FieldOfView = _currentFOV;
	}

	// ── Third Person (GTA V style — free look + auto-center) ────────────────

	private void UpdateThirdPerson( float dt, float speed )
	{
		float vehicleYaw = Vehicle.WorldRotation.Yaw();

		// ── Mouse free look (consume accumulated input) ──────────
		Vector2 mouseDelta = _pendingMouseDelta;
		_pendingMouseDelta = Vector2.Zero;
		bool hasMouseInput = mouseDelta.LengthSquared > 0.5f;

		if ( hasMouseInput )
		{
			_orbitYaw -= mouseDelta.x * LookSensitivity;
			_orbitPitch += mouseDelta.y * LookSensitivity;
			_orbitPitch = _orbitPitch.Clamp( MinPitch, MaxTPPitch );
			_timeSinceMouseMove = 0f;
		}
		else
		{
			_timeSinceMouseMove += dt;
		}

		// ── Auto-center back behind vehicle after delay ──────────
		if ( _timeSinceMouseMove > AutoCenterDelay )
		{
			float centerStrength = dt * AutoCenterSpeed;
			_orbitYaw = MathX.Lerp( _orbitYaw, 0f, centerStrength );
			_orbitPitch = MathX.Lerp( _orbitPitch, DefaultPitch, centerStrength );
		}

		// ── Final camera yaw = vehicle yaw + orbit offset ────────
		_camRotation = Rotation.From( _orbitPitch, vehicleYaw + _orbitYaw, 0f );

		// ── Distance (speed-adaptive) ────────────────────────────
		float targetDist = speed.MapRange( 0f, 3000f, BaseDistance, MaxDistance );
		_distance = MathX.Lerp( _distance, targetDist, dt * 10f );

		// ── Position ─────────────────────────────────────────────
		Vector3 pivot = Vehicle.WorldPosition + Vector3.Up * HeightOffset;
		Vector3 camPos = pivot + _camRotation.Backward * _distance;

		// Wall collision
		var tr = Scene.Trace.Ray( pivot, camPos )
			.IgnoreGameObjectHierarchy( Vehicle.GameObject )
			.WithoutTags( "trigger", "player", "ragdoll", "wheel" )
			.Run();

		if ( tr.Hit )
			camPos = tr.EndPosition + tr.Normal * 5f;

		_camera.WorldPosition = camPos;
		_camera.WorldRotation = _camRotation;
	}

	// ── First Person: head bone + Neck FX ────────────────────────────────────

	private void UpdateFirstPerson( float dt, float lateralG, float longitudinalG )
	{
		// ── Mouse look (accumulated in OnUpdate) ─────────────────────
		_fpEyeAngles = _fpEyeAngles.WithPitch( _fpEyeAngles.pitch.Clamp( -60f, 60f ) );

		// ── Get eye position from sitting animation skeleton ─────────
		Vector3 headPos = Vehicle.WorldPosition + Vector3.Up * 40f; // fallback
		if ( _localPlayer.IsValid() && _localPlayer.Body.IsValid() && _localPlayer.BodyRenderer.IsValid() )
		{
			var sceneModel = _localPlayer.BodyRenderer.SceneModel;
			var eyeL = sceneModel.GetBoneWorldTransform( "eye_L" ).Position;
			var eyeR = sceneModel.GetBoneWorldTransform( "eye_R" ).Position;
			headPos = (eyeL + eyeR) * 0.5f;
			// Push camera slightly forward to avoid seeing inside the head
			headPos += Vehicle.WorldTransform.Forward * 3f;
		}

		// ── Neck FX: smooth g-force offsets ───────────────────────────
		float targetLat = (lateralG * LateralGain).Clamp( -MaxLateralOffset, MaxLateralOffset );
		float targetLong = (-longitudinalG * LongitudinalGain).Clamp( -MaxLongitudinalOffset, MaxLongitudinalOffset );
		_smoothGOffset = Vector2.Lerp( _smoothGOffset, new Vector2( targetLat, targetLong ), dt * GForceSmoothing );

		float targetRoll = (lateralG * RollGain).Clamp( -MaxRoll, MaxRoll );
		_smoothRoll = MathX.Lerp( _smoothRoll, targetRoll, dt * GForceSmoothing );

		float targetPitch = (-longitudinalG * PitchGain).Clamp( -MaxPitch, MaxPitch );
		_smoothPitch = MathX.Lerp( _smoothPitch, targetPitch, dt * GForceSmoothing );

		float steerAngle = Vehicle.Components.Get<Steering>()?.CurrentAngle ?? 0f;
		float lookAhead = steerAngle * LookAheadGain;

		// ── Position: head bone + g-force offset ─────────────────────
		headPos += Vehicle.WorldTransform.Right * _smoothGOffset.x;
		headPos += Vehicle.WorldTransform.Forward * _smoothGOffset.y;

		// ── Rotation: mouse look + neck FX ───────────────────────────
		Rotation neckFX = Rotation.From( _smoothPitch, lookAhead, _smoothRoll );
		_camRotation = _fpEyeAngles.ToRotation() * neckFX;

		_camera.WorldPosition = headPos;
		_camera.WorldRotation = _camRotation;
	}

	/// <summary>
	/// In first person vehicle: keep body visible so IK (hands on wheel, feet on pedals) is seen.
	/// The camera forward offset prevents seeing inside the head.
	/// </summary>
	private void UpdatePlayerVisibility()
	{
		if ( !_localPlayer.IsValid() || !_localPlayer.Body.IsValid() )
			return;

		// Always show the full body in vehicle (both first and third person)
		_localPlayer.Body.Tags.Set( "viewer", false );

		if ( _camera.IsValid() )
			_camera.RenderExcludeTags.Set( "viewer", false );
	}

	// ── Utilities ────────────────────────────────────────────────────────────

	private PlayerPawn GetLocalSeatedPlayer()
	{
		if ( Vehicle.Seats == null ) return null;

		foreach ( var seat in Vehicle.Seats )
		{
			if ( !seat.IsValid() || !seat.Player.IsValid() )
				continue;

			if ( seat.Player.IsLocallyControlled )
				return seat.Player;
		}

		return null;
	}

	private void Activate( PlayerPawn player )
	{
		_playerCamera = player.CameraController;
		_camera = _playerCamera.Camera;
		_audioListener = _camera?.GetComponent<AudioListener>();

		_previousCameraMode = _playerCamera.Mode;
		_playerCamera.Mode = CameraMode.ThirdPerson;
		_playerCamera.SetActive( false );

		if ( _camera.IsValid() )
			_camera.GameObject.Enabled = true;

		_camRotation = Rotation.From( DefaultPitch, Vehicle.WorldRotation.Yaw(), 0f );
		_orbitYaw = 0f;
		_orbitPitch = DefaultPitch;
		_timeSinceMouseMove = AutoCenterDelay + 1f;
		_fpEyeAngles = new Angles( 0f, Vehicle.WorldRotation.Yaw(), 0f );
		_distance = BaseDistance;
		_currentFOV = BaseFOV;
		_prevVelocity = Vehicle.Rigidbody.IsValid() ? Vehicle.Rigidbody.Velocity : Vector3.Zero;
		_smoothAccel = Vector3.Zero;
		_smoothGOffset = Vector2.Zero;
		_smoothRoll = 0f;
		_smoothPitch = 0f;
		_firstPerson = false;
		_active = true;

		// Ensure body is fully visible when entering (third person default)
		UpdatePlayerVisibility();
	}

	private void Deactivate()
	{
		// Restore full body visibility
		if ( _localPlayer.IsValid() && _localPlayer.Body.IsValid() )
			_localPlayer.Body.Tags.Set( "viewer", false );

		if ( _playerCamera.IsValid() )
		{
			_playerCamera.Mode = _previousCameraMode;
			_playerCamera.SetActive( true );
		}

		_camera = null;
		_playerCamera = null;
		_localPlayer = null;
		_active = false;
	}
}
