using Sandbox;
using System;

namespace OpenFramework;

[Title( "Wheel Assembly" )]
[Category( "Vehicle" )]
public sealed class WheelAssembly : Component
{
	[Property] public bool IsSteerable { get; set; } = false;
	[Property] public bool IsDriven { get; set; } = true;
	[Property] public bool IsRear { get; set; } = false;
	[Property] public GameObject WheelMesh { get; set; }
	[Property] public bool DebugLog { get; set; } = false;

	public float CurrentSteerAngle { get; private set; }
	public bool IsGrounded { get; private set; }
	public float DebugSuspForce { get; private set; }

	private Rigidbody _rb;
	private VehicleInformation _info;
	private float _spinDeg;
	private Vector3 _meshRestLocalPos;

	public void Init( Rigidbody rb, VehicleInformation info )
	{
		_rb = rb;
		_info = info;
		_meshRestLocalPos = WheelMesh.IsValid() ? WheelMesh.LocalPosition : Vector3.Zero;
	}

	public void Tick( float throttle, bool braking, bool handbrake, float targetSteer, float dt )
	{
		if ( _rb == null || _info == null ) return;

		// ── Braquage ──────────────────────────────────────────────────────
		CurrentSteerAngle = IsSteerable
			? MathX.Lerp( CurrentSteerAngle, targetSteer, MathF.Min( _info.SteerSpeed * dt, 1f ) )
			: 0f;

		// ── Raycast ───────────────────────────────────────────────────────
		var origin = WorldPosition;
		var tr = Scene.Trace
			.Ray( origin, origin + Vector3.Down * _info.SuspensionLength )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.Run();

		IsGrounded = tr.Hit;
		DebugSuspForce = 0f;

		AnimateMesh( tr, dt );
		if ( !IsGrounded ) return;

		// ── Suspension ────────────────────────────────────────────────────
		float compression = _info.SuspensionLength - tr.Distance;
		if ( compression > 0f )
		{
			float compVel = Vector3.Dot( _rb.GetVelocityAtPoint( origin ), Vector3.Up );
			float suspForce = MathF.Max( _info.SpringStrength * compression - _info.SpringDamper * compVel, 0f );
			DebugSuspForce = suspForce;
			_rb.ApplyForceAt( origin, Vector3.Up * suspForce );
		}

		// ── Axes depuis le Rigidbody ───────────────────────────────────────
		var rbRot = _rb.WorldRotation;
		var steerRot = Rotation.FromAxis( rbRot.Up, CurrentSteerAngle );
		var fwd = steerRot * rbRot.Forward;
		var right = steerRot * rbRot.Right;

		var vel = _rb.GetVelocityAtPoint( origin );
		float velFwd = Vector3.Dot( vel, fwd );
		float velSide = Vector3.Dot( vel, right );

		// ── Grip latéral ─────────────────────────────────────────────────
		// Seulement si on n'accélère pas — évite d'annuler la traction
		if ( MathF.Abs( throttle ) < 0.01f )
		{
			float gripMult = (IsRear && handbrake) ? _info.HandbrakeGrip : 1f;
			_rb.ApplyForceAt( origin, right * (-velSide * _info.LateralGripForce * gripMult) );
		}

		// ── Traction / freinage ───────────────────────────────────────────
		float drive = 0f;
		CurrentDriveForce = drive; // Stocke la force pour les Gizmos
		_rb.ApplyForceAt( origin, fwd * drive );
		if ( IsRear && handbrake )
		{
			drive = -MathF.Sign( velFwd ) * _info.HandbrakeForce;
		}
		else if ( braking )
		{
			drive = -MathF.Sign( velFwd ) * _info.BrakeForce;
		}
		else if ( IsDriven && MathF.Abs( throttle ) > 0.01f )
		{
			float speedRatio = MathF.Min( _rb.Velocity.Length / _info.MaxSpeed, 1f );
			drive = throttle * _info.EnginePower * (1f - speedRatio);
		}
		else if ( IsDriven && MathF.Abs( velFwd ) > 0.1f )
		{
			// Engine braking seulement si on roule déjà
			drive = -velFwd * _info.EngineBraking;
		}

		if ( MathF.Abs( drive ) > 0.01f )
		{
			_rb.ApplyForceAt( origin, fwd * drive );

			if ( DebugLog )
				Log.Info( $"[{GameObject.Name}] drive={drive:F0}N | vel={_rb.Velocity.Length:F2} u/s" );
		}
	}

	private void AnimateMesh( SceneTraceResult tr, float dt )
	{
		if ( !WheelMesh.IsValid() || _rb == null ) return;

		var rbRot = _rb.WorldRotation;
		var fwd = Rotation.FromAxis( rbRot.Up, CurrentSteerAngle ) * rbRot.Forward;
		float velFwd = Vector3.Dot( _rb.GetVelocityAtPoint( WorldPosition ), fwd );
		float circ = 2f * MathF.PI * MathF.Max( _info?.WheelRadius ?? 20f, 0.1f );
		_spinDeg += (velFwd / circ * 360f) * dt;

		WheelMesh.LocalRotation = Rotation.FromYaw( CurrentSteerAngle )
								* Rotation.FromPitch( _spinDeg );

		float suspended = tr.Hit ? MathF.Max( (_info?.SuspensionLength ?? 20f) - tr.Distance, 0f ) : 0f;
		WheelMesh.LocalPosition = _meshRestLocalPos + new Vector3( 0f, 0f, -suspended );
	}// Ajoute ceci pour suivre la force appliquée
	public float CurrentDriveForce { get; private set; }

	protected override void DrawGizmos()
	{
		var origin = WorldPosition;
		float len = _info?.SuspensionLength ?? 20f;

		// 1. Suspension (Rouge si pas au sol, Vert si au sol)
		Gizmo.Draw.Color = IsGrounded ? Color.Green : Color.Red;
		Gizmo.Draw.LineThickness = 2f;
		Gizmo.Draw.Line( origin, origin + Vector3.Down * len );

		// 2. Visualiser la force de suspension (Bleu)
		Gizmo.Draw.Color = Color.Blue;
		Gizmo.Draw.Line( origin, origin + Vector3.Up * (DebugSuspForce / 1000f) );

		// 3. Visualiser la force motrice (Vert pour avancer, Rouge pour freiner)
		Gizmo.Draw.Color = CurrentDriveForce >= 0 ? Color.Green : Color.Red;
		var fwd = WorldRotation.Forward;
		Gizmo.Draw.Line( origin, origin + fwd * (CurrentDriveForce / 500f) );
	}
}
