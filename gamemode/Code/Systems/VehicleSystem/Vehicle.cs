using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenFramework;

[Title( "Vehicle Controller" )]
[Category( "Vehicle" )]
public sealed class VehicleController : Component, IUse
{
	[Property] public VehicleInformation Information { get; set; }
	[Property] public GameObject WheelsRoot { get; set; }
	[Property] public CameraComponent DriverCamera { get; set; }

	[Property] public bool TestDriveMode { get; set; } = true;
	[Property] public bool ShowDebugLog { get; set; } = true;

	[Sync] public float SyncThrottle { get; private set; }
	[Sync] public float SyncSteer { get; private set; }
	[Sync] public bool SyncBrake { get; private set; }
	[Sync] public bool SyncHandbrake { get; private set; }
	[Sync] public ulong DriverId { get; private set; }

	private List<WheelAssembly> _wheels;
	private Rigidbody _rb;
	private PlayerPawn _driver;
	private CameraComponent _driverOriginalCam;
	private float _logTimer;

	public bool HasDriver => _driver != null;

	protected override void OnStart()
	{
		_rb = Components.Get<Rigidbody>();
		if ( !_rb.IsValid() )
			_rb = Components.Get<Rigidbody>( FindMode.InDescendants );

		if ( !_rb.IsValid() ) { Log.Error( "[Vehicle] Rigidbody manquant." ); return; }
		if ( Information == null ) { Log.Error( "[Vehicle] VehicleInformation manquant." ); return; }
		if ( !WheelsRoot.IsValid() ) { Log.Error( "[Vehicle] WheelsRoot manquant." ); return; }

		_rb.MassOverride = Information.Mass;
		_rb.MotionEnabled = true;
		_rb.LinearDamping = 0f;
		_rb.AngularDamping = 0.5f;

		_wheels = WheelsRoot.Components
			.GetAll<WheelAssembly>( FindMode.InDescendants )
			.ToList();

		foreach ( var w in _wheels )
			w.Init( _rb, Information );

		if ( DriverCamera.IsValid() )
			DriverCamera.Enabled = false;

		Log.Info( $"[Vehicle] Prêt — {_wheels.Count} roues." );
	}

	protected override void OnFixedUpdate()
	{
		if ( _wheels == null || !_rb.IsValid() || Information == null ) return;

		// Lit l'input si on est en TestDriveMode OU si on est le conducteur owner
		bool isOwner = TestDriveMode || (!IsProxy && HasDriver);

		if ( isOwner )
		{
			float throttle = 0f;
			if ( Input.Down( "Forward" ) ) throttle = 1f;
			if ( Input.Down( "Backward" ) ) throttle = -1f;

			float steer = 0f;
			if ( Input.Down( "Left" ) ) steer = -1f;
			if ( Input.Down( "Right" ) ) steer = 1f;

			float speed = _rb.Velocity.Length;
			float speedRatio = MathF.Min( speed / MathF.Max( Information.MaxSpeed, 1f ), 1f );

			SyncThrottle = throttle;
			SyncSteer = steer * Information.MaxSteerAngle * (1f - speedRatio * Information.SteerLimitAtSpeed);
			SyncBrake = Input.Down( "brake" );
			SyncHandbrake = Input.Down( "Jump" );

			if ( ShowDebugLog && (MathF.Abs( throttle ) > 0f || MathF.Abs( steer ) > 0f) )
				Log.Info( $"[Vehicle] T:{throttle:F0} S:{steer:F0} | vel:{speed:F1} u/s | Sol:{_wheels.Count( w => w.IsGrounded )}/{_wheels.Count}" );
		}

		float dt = Time.Delta;
		foreach ( var w in _wheels )
			w.Tick( SyncThrottle, SyncBrake, SyncHandbrake, SyncSteer, dt );

		// Anti-tonneau
		float tilt = Vector3.Dot( WorldRotation.Up, Vector3.Right );
		if ( MathF.Abs( tilt ) > 0.1f )
			_rb.ApplyTorque( WorldRotation.Forward * (-tilt * Information.Mass * 40f) );
	}

	protected override void OnUpdate()
	{
		if ( HasDriver && Input.Pressed( "use" ) )
			ExitVehicle();

		if ( !ShowDebugLog || _rb == null || _wheels == null ) return;
		_logTimer += Time.Delta;
		if ( _logTimer < 2f ) return;
		_logTimer = 0f;
		Log.Info( $"[Vehicle] {_rb.Velocity.Length:F0} u/s | Sol:{_wheels.Count( w => w.IsGrounded )}/{_wheels.Count} | T:{SyncThrottle:F2} S:{SyncSteer:F1}°" );
	}

	public UseResult CanUse( PlayerPawn player ) => !HasDriver;

	public void OnUse( PlayerPawn player )
	{
		if ( HasDriver ) return;

		_driver = player;
		DriverId = player.SteamId;

		foreach ( var col in player.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			col.Enabled = false;

		_driverOriginalCam = player.Components
			.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( c => c.Enabled );

		if ( _driverOriginalCam.IsValid() ) _driverOriginalCam.Enabled = false;
		if ( DriverCamera.IsValid() ) DriverCamera.Enabled = true;

		player.IsFrozen = true;
		// NE PAS mettre TestDriveMode = false ici — ça coupait l'input

		//GameObject.Network.AssignOwnership( player.Network.OwnerConnection );
		Log.Info( "[Vehicle] Conducteur entré." );
	}

	private void ExitVehicle()
	{
		if ( _driver == null ) return;

		foreach ( var col in _driver.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
			col.Enabled = true;

		if ( _driverOriginalCam.IsValid() ) _driverOriginalCam.Enabled = true;
		if ( DriverCamera.IsValid() ) DriverCamera.Enabled = false;

		_driver.IsFrozen = false;
		_driver = null;
		DriverId = 0;
		_driverOriginalCam = null;
		SyncThrottle = SyncSteer = 0f;
		SyncBrake = SyncHandbrake = false;

		GameObject.Network.DropOwnership();
		Log.Info( "[Vehicle] Conducteur sorti." );
	}
}
