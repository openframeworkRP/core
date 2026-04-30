using System;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Pacejka "Magic Formula" parameters for tire force curves.
/// F = D * sin( C * atan( B*x - E*(B*x - atan(B*x)) ) )
/// </summary>
public class PacejkaParams
{
	[Property] public float B { get; set; } = 10.0f;  // Stiffness
	[Property] public float C { get; set; } = 2.0f;   // Shape
	[Property] public float D { get; set; } = 0.85f;  // Peak
	[Property] public float E { get; set; } = 0.97f;  // Curvature
}

[Category( "Vehicles" )]
[Title( "Wheel" )]
[Icon( "sync" )]
public sealed class Wheel : Component
{
	// ── Visual ────────────────────────────────────────────────────────────────
	/// <summary>Wheel model to render (optional).</summary>
	[Property, Group( "Visual" )] public Model Model { get; set; }

	/// <summary>Flip the wheel model 180 on the yaw axis (for left-side wheels).</summary>
	[Property, Group( "Visual" )] public bool RenderFlipped { get; set; } = false;

	// ── Suspension ────────────────────────────────────────────────────────────
	/// <summary>Suspension rest length in inches.</summary>
	[Property, Group( "Suspension" )] public float RestLength { get; set; } = 12.0f;

	/// <summary>Wheel radius in inches.</summary>
	[Property, Group( "Suspension" )] public float Radius { get; set; } = 10.7f;

	/// <summary>Wheel width (thickness) in inches.</summary>
	[Property, Group( "Suspension" )] public float Width { get; set; } = 7.0f;

	/// <summary>Spring stiffness.</summary>
	[Property, Group( "Suspension" )] public float SpringStiffness { get; set; } = 6000f;

	/// <summary>Damper stiffness.</summary>
	[Property, Group( "Suspension" )] public float DamperStiffness { get; set; } = 55f;

	// ── Tire ──────────────────────────────────────────────────────────────────

	/// <summary>Optional tire preset. Click "Apply Preset" to copy its values.</summary>
	[Property, Group( "Tire" )] public TirePreset TirePreset { get; set; }

	/// <summary>Copies all values from the assigned tire preset.</summary>
	[Button( "Apply Tire Preset" )]
	public void ApplyTirePreset()
	{
		if ( TirePreset == null )
		{
			Log.Warning( "Wheel: No tire preset assigned." );
			return;
		}

		FrictionMu = TirePreset.FrictionMu;
		LoadSensitivity = TirePreset.LoadSensitivity;
		NominalLoad = TirePreset.NominalLoad;
		RollingResistance = TirePreset.RollingResistance;
		LatPacejka = new PacejkaParams
		{
			B = TirePreset.LatB,
			C = TirePreset.LatC,
			D = TirePreset.LatD,
			E = TirePreset.LatE
		};
		LongPacejka = new PacejkaParams
		{
			B = TirePreset.LongB,
			C = TirePreset.LongC,
			D = TirePreset.LongD,
			E = TirePreset.LongE
		};

		Log.Info( $"Wheel: Applied tire preset '{TirePreset.DisplayName}'" );
	}

	/// <summary>Friction coefficient (surface grip multiplier).</summary>
	[Property, Group( "Tire" )] public float FrictionMu { get; set; } = 0.9f;

	/// <summary>Load sensitivity exponent for power-based grip model.</summary>
	[Property, Group( "Tire" )] public float LoadSensitivity { get; set; } = 0.8f;

	/// <summary>Nominal load used to compute GripScale.</summary>
	[Property, Group( "Tire" )] public float NominalLoad { get; set; } = 1400f;

	/// <summary>Wheel rotational inertia in kg*m^2.</summary>
	[Property, Group( "Tire" )] public float Inertia { get; set; } = 1.5f;

	/// <summary>Rolling resistance coefficient.</summary>
	[Property, Group( "Tire" )] public float RollingResistance { get; set; } = 0.02f;

	/// <summary>Relaxation length (shared lateral/longitudinal). Units: mm internally, divided by 1000 for use.</summary>
	[Property, Group( "Tire" )] public float RelaxationLength { get; set; } = 2.5f;

	/// <summary>Lateral Pacejka parameters.</summary>
	[Property, Group( "Tire" )] public PacejkaParams LatPacejka { get; set; } = new()
	{
		B = 5.0f,
		C = 1.2f,
		D = 1.0f,
		E = 0.92f
	};

	/// <summary>Longitudinal Pacejka parameters.</summary>
	[Property, Group( "Tire" )] public PacejkaParams LongPacejka { get; set; } = new()
	{
		B = 7.0f,
		C = 2.0f,
		D = 1.0f,
		E = 0.92f
	};

	// ── Ratios (set by Vehicle / Axle) ───────────────────────────────────────
	/// <summary>Steering ratio: 1.0 for front wheels that steer, 0 for rear.</summary>
	[Property, Group( "Setup" )] public float SteeringRatio { get; set; } = 0f;

	/// <summary>Driving ratio: fraction of drive torque this wheel receives.</summary>
	[Property, Group( "Setup" )] public float DrivingRatio { get; set; } = 1.0f;

	// ── Inputs (set by Vehicle each tick) ────────────────────────────────────
	/// <summary>Drive torque in N*m applied by the drivetrain.</summary>
	public float DriveTorque { get; set; }

	/// <summary>Brake torque in N*m.</summary>
	public float BrakeTorque { get; set; }

	/// <summary>Steer angle in degrees, set by the steering component each frame.</summary>
	[Sync( SyncFlags.FromHost )] public float SteerAngle { get; set; }

	/// <summary>Friction coefficient alias (used by Vehicle parking friction).</summary>
	public float Mu => FrictionMu;

	/// <summary>Per-tire force budget set by Vehicle each tick. Not used by lodzero tire model directly.</summary>
	public float ForceBudget { get; set; }

	/// <summary>Set by Vehicle when drive torque is nonzero.</summary>
	public bool VehicleHasDriveInput { get; set; }

	/// <summary>Multiplier for lateral (Fy) force. Set &lt; 1 to reduce side grip (e.g. on trailers).</summary>
	public float LateralGripScale { get; set; } = 1f;

	// ── State (readable by Vehicle / HUD) ────────────────────────────────────
	[Sync( SyncFlags.FromHost )] public bool    IsGrounded        { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   Compression       { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   Fz                { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   Fx                { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   Fy                { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   SlipRatio         { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   SlipAngle         { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   DynamicSlipRatio  { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   DynamicSlipAngle  { get; private set; }
	public float   AngularVelocity   { get; set; }
	public float   HitDistance       { get; private set; }
	public float   RollAngle         { get; private set; }
	[Sync( SyncFlags.FromHost )] public Vector3 ContactNormal     { get; private set; }
	[Sync( SyncFlags.FromHost )] public Vector3 ContactPosition   { get; private set; }
	[Sync( SyncFlags.FromHost )] public float   Grip              { get; private set; }

	/// <summary>The surface resource hit by this wheel's trace (null if airborne).</summary>
	public Surface HitSurface { get; private set; }

	// ── Debug ─────────────────────────────────────────────────────────────────
	[Property, Group( "Debug" )] public bool ShowForceGizmos { get; set; } = false;

	/// <summary>Enable periodic console logging of wheel state.</summary>
	[Property, Group( "Debug" )] public bool LogToConsole { get; set; } = false;

	/// <summary>Current suspension length in inches (for visual positioning).</summary>
	public float LastLength { get; set; }

	// ── Internals ─────────────────────────────────────────────────────────────
	private Rigidbody _rb;
	private float _sx;
	private float _sy;
	private Vector3 _localVel;

	// Cached grip scale: (NominalLoad * FrictionMu) / Pow(NominalLoad, LoadSensitivity)
	private float _gripScale;

	// Debug vectors
	private Vector3 _dbgSuspForce;
	private Vector3 _dbgLatForce;
	private Vector3 _dbgLongForce;

	// The effective world transform of the wheel hub (includes steering rotation)
	private Transform _wheelTransform;

	protected override void OnEnabled()
	{
		_rb = Components.GetInAncestorsOrSelf<Rigidbody>();
		LastLength = RestLength;
		RecalcGripScale();
	}

	private void RecalcGripScale()
	{
		if ( NominalLoad > 0f )
			_gripScale = (NominalLoad * FrictionMu) / MathF.Pow( NominalLoad, LoadSensitivity );
		else
			_gripScale = FrictionMu;
	}

	/// <summary>
	/// Power-based load sensitivity model from lodzero.
	/// Returns available grip force for a given normal load.
	/// </summary>
	private float GetAvailableGrip( float load )
	{
		if ( load <= 0f ) return 0f;
		return _gripScale * MathF.Pow( load, LoadSensitivity );
	}

	/// <summary>
	/// Lateral Pacejka: input is slip angle in degrees, converted to radians internally.
	/// </summary>
	private float GetLateralTraction( float slipAngleDeg )
	{
		float sa = slipAngleDeg * MathF.PI / 180f;
		float bx = LatPacejka.B * sa;
		float result = LatPacejka.D * MathF.Sin( LatPacejka.C * MathF.Atan( bx - LatPacejka.E * (bx - MathF.Atan( bx )) ) );
		return MathX.Clamp( result, -1f, 1f );
	}

	/// <summary>
	/// Longitudinal Pacejka: input is slip ratio (dimensionless).
	/// </summary>
	private float GetLongitudinalTraction( float slipRatio )
	{
		float bx = LongPacejka.B * slipRatio;
		float result = LongPacejka.D * MathF.Sin( LongPacejka.C * MathF.Atan( bx - LongPacejka.E * (bx - MathF.Atan( bx )) ) );
		return MathX.Clamp( result, -1f, 1f );
	}

	/// <summary>
	/// Auto-calculates wheel radius from the Model's bounding box.
	/// Uses half the Z-extent of the model bounds (in inches).
	/// </summary>
	[Button( "Auto Radius from Model" )]
	public void AutoCalculateRadius()
	{
		if ( Model == null )
		{
			Log.Warning( "Wheel: No Model assigned, can't auto-calculate radius." );
			return;
		}

		var bounds = Model.Bounds;
		float diameterInches = bounds.Size.z;
		if ( diameterInches <= 0f ) return;

		Radius = diameterInches * 0.5f;
		Log.Info( $"Wheel: Auto-calculated radius = {Radius:F2} inches from model bounds ({diameterInches:F1} inches height)" );
	}

	/// <summary>
	/// Called each physics tick by the Vehicle.
	/// Computes suspension, tire forces, and updates wheel spin.
	/// When visualOnly is true (client-side), only computes wheel state for rendering — no forces applied.
	/// </summary>
	public void Step( bool visualOnly = false )
	{
		if ( !_rb.IsValid() ) return;

		if ( !visualOnly )
			RecalcGripScale();

		float deltaTime = Time.Delta;
		if ( deltaTime <= 0f ) return;

		// Build the wheel hub transform: base position + steering rotation
		_wheelTransform = WorldTransform;
		_wheelTransform.Rotation = WorldTransform.RotationToWorld(
			Rotation.From( 0f, SteerAngle * SteeringRatio, 0f )
		);

		// ── Single cylinder trace (lodzero style) ────────────────────────
		Vector3 start = _wheelTransform.Position;
		Vector3 end = start + _wheelTransform.Down * RestLength;

		var hit = Scene.Trace
			.Cylinder( Width, Radius, start, end )
			.Rotated( Rotation.FromAxis( _wheelTransform.Forward, 90f ) )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "Vehicle", "car", "vehicle", "player" )
			.UseHitPosition( true )
			.Run();

		if ( hit.Hit )
		{
			Vector3 dir = hit.StartedSolid ? _wheelTransform.Down : hit.Direction;
			Vector3 wheelPos = hit.StartPosition + dir * hit.Distance;

			ContactPosition = wheelPos - Vector3.VectorPlaneProject( hit.Normal, _wheelTransform.Right ) * Radius;
			ContactNormal = hit.Normal;
			HitDistance = hit.Distance;

			// ── Suspension geometry (needed for visuals on all machines) ──
			Compression = 1f - hit.Distance / RestLength;
			IsGrounded = true;
			HitSurface = hit.Surface;

			if ( !visualOnly )
			{
				// ── Host only: compute and apply physics forces ──────────
				float currLength = Vector3.DistanceBetween(
					hit.HitPosition + _wheelTransform.Up * Radius,
					hit.StartPosition
				);

				float springForce = Compression * SpringStiffness;
				float velocity = (LastLength - currLength) / deltaTime;
				float damperForce = velocity * DamperStiffness;
				LastLength = currLength;
				Fz = springForce + damperForce;

				// Suspension can only push, never pull
				Fz = MathF.Max( Fz, 0f );

				// Non-linear suspension force (lodzero's power curve)
				if ( Fz > 0f )
				{
					float fzMax = SpringStiffness;
					float normalizedFz = MathX.Clamp( Fz / fzMax, 0f, 1f );
					Fz = MathF.Pow( normalizedFz, 0.8f ) * fzMax;
				}

				// Apply suspension force
				Vector3 suspForce = hit.Normal * Fz * 100f;
				_rb.ApplyForceAt( ContactPosition, suspForce );
				_dbgSuspForce = hit.Normal * Fz;

				// ── Traction ─────────────────────────────────────────
				ComputeTireForces( ref hit, deltaTime );
			}
		}
		else
		{
			IsGrounded = false;
			Compression = 0f;
			HitDistance = RestLength;
			ContactNormal = Vector3.Up;
			ContactPosition = _wheelTransform.Position;

			if ( !visualOnly )
			{
				Fz = 0f;
				Fx = 0f;
				Fy = 0f;
				Grip = 0f;
				HitSurface = null;
				LastLength = RestLength;
				_dbgSuspForce = Vector3.Zero;
				_dbgLatForce = Vector3.Zero;
				_dbgLongForce = Vector3.Zero;
			}
		}

		// 1 km/h ≈ 10.94 inches/s — below this, stop wheel animation
		bool vehicleTooSlow = _rb.Velocity.Length < 10.94f;

		if ( visualOnly )
		{
			// Client: estimate wheel spin from vehicle linear velocity
			float r = Radius * 0.0254f;

			if ( vehicleTooSlow )
			{
				AngularVelocity = 0f;
			}
			else
			{
				float vel = Vector3.Dot( _rb.Velocity, _wheelTransform.Forward ) * 0.0254f;
				AngularVelocity = r > 0.01f ? vel / r : 0f;
			}

			RollAngle += MathX.RadianToDegree( AngularVelocity ) * deltaTime;
			RollAngle = (RollAngle % 360f + 360f) % 360f;
		}
		else
		{
			UpdateRotation( deltaTime );
			LogState();
		}
	}

	/// <summary>
	/// Computes traction forces using lodzero's weighted friction circle model
	/// with relaxation-based slip calculations.
	/// </summary>
	private void ComputeTireForces( ref SceneTraceResult hit, float deltaTime )
	{
		Vector3 projForward = Vector3.VectorPlaneProject( _wheelTransform.Forward, ContactNormal ).Normal;
		Vector3 projLeft = Vector3.VectorPlaneProject( _wheelTransform.Left, ContactNormal ).Normal;

		// Velocity at contact point, accounting for hit body velocity
		Vector3 combinedVel = _rb.GetVelocityAtPoint( ContactPosition );
		if ( hit.Body.IsValid() )
			combinedVel -= hit.Body.GetVelocityAtPoint( ContactPosition );
		if ( hit.Collider.IsValid() )
			combinedVel -= hit.Collider.SurfaceVelocity;

		// Convert to local wheel space and then to m/s
		_localVel = combinedVel * _wheelTransform.Rotation.Inverse;
		_localVel *= 0.0254f; // inches to m/s

		// Road friction from surface with fallback per surface type
		float roadFrictionCoeff = 0.95f;
		if ( hit.Surface is not null )
		{
			roadFrictionCoeff = hit.Surface.Friction;

			// Extra grip reduction for off-road surfaces
			string surfName = hit.Surface.ResourceName?.ToLowerInvariant() ?? "";
			if ( surfName.Contains( "grass" ) || surfName.Contains( "dirt" ) || surfName.Contains( "mud" ) )
				roadFrictionCoeff *= 0.75f;
			else if ( surfName.Contains( "sand" ) || surfName.Contains( "gravel" ) || surfName.Contains( "snow" ) )
				roadFrictionCoeff *= 0.6f;
			else if ( surfName.Contains( "ice" ) )
				roadFrictionCoeff *= 0.2f;
		}
		roadFrictionCoeff += 0.2f;

		// ── Slip calculations with relaxation ────────────────────────
		CalcSy( deltaTime );
		CalcSx( deltaTime );

		// ── Weighted friction circle (lodzero) ───────────────────────
		Grip = GetAvailableGrip( Fz ) * roadFrictionCoeff;

		float xWeight = 0.8f;
		float yWeight = 1.2f;

		Vector2 weightedForce = new Vector2( SlipRatio / xWeight, _sy / yWeight );
		if ( weightedForce.Length > 1f )
			weightedForce = weightedForce.Normal;

		Fx = Grip * weightedForce.x * xWeight;
		Fy = Grip * weightedForce.y * yWeight * LateralGripScale;

		// ── Apply tire forces ────────────────────────────────────────
		Vector3 longitudinalForce = projForward * Fx;
		Vector3 lateralForce = projLeft * Fy;
		_rb.ApplyForceAt( ContactPosition, (lateralForce + longitudinalForce) * 100f );

		_dbgLongForce = projForward * Fx;
		_dbgLatForce = projLeft * Fy;
	}

	/// <summary>
	/// Lateral slip calculation with relaxation (lodzero model).
	/// </summary>
	private void CalcSy( float deltaTime )
	{
		float sideVel = _localVel.y / -1f;
		float slipAngle = _localVel.x == 0f
			? 0f
			: MathX.RadianToDegree( MathF.Atan( sideVel / MathF.Abs( _localVel.x ) ) );

		float relaxLen = RelaxationLength / 1000f;
		float coeff = 1f - MathF.Exp( -MathF.Abs( _localVel.x ) * deltaTime / relaxLen );

		float alpha = _localVel.Length.MapRange( 0.5f, 1f, 0f, 1f );
		float targetSlip = MathX.Lerp( 3f * MathF.Sign( sideVel ), slipAngle, alpha );
		float diff = targetSlip - DynamicSlipAngle;

		DynamicSlipAngle = MathX.Clamp( DynamicSlipAngle + diff * coeff, -90f, 90f );
		SlipAngle = DynamicSlipAngle;

		_sy = GetLateralTraction( DynamicSlipAngle );
	}

	/// <summary>
	/// Longitudinal slip calculation with relaxation (lodzero model).
	/// </summary>
	private void CalcSx( float deltaTime )
	{
		float r = Radius * 0.0254f; // inches to meters
		float grip = GetAvailableGrip( Fz );
		if ( grip <= 0f )
		{
			SlipRatio = 0f;
			DynamicSlipRatio = 0f;
			_sx = 0f;
			return;
		}

		float target = ((AngularVelocity - _localVel.x / r) / deltaTime * Inertia) / (grip * r);

		float relaxLen = RelaxationLength / 1000f;
		float coeff = 1f - MathF.Exp( -MathF.Abs( _localVel.x ) * deltaTime / relaxLen );
		DynamicSlipRatio += (target - DynamicSlipRatio) * coeff;
		SlipRatio = Fz == 0f ? 0f : DynamicSlipRatio;

		_sx = GetLongitudinalTraction( SlipRatio );
	}

	/// <summary>
	/// Updates wheel angular velocity based on drive torque, brake torque,
	/// friction torque, and rolling resistance (lodzero model).
	/// </summary>
	private void UpdateRotation( float deltaTime )
	{
		float r = Radius * 0.0254f; // inches to meters

		// Drive torque + friction torque
		float frictionTorque = Fx * r;
		float driveAccel = (DriveTorque - frictionTorque) / Inertia;
		AngularVelocity += driveAccel * deltaTime;

		// Braking and rolling resistance
		float sign = MathF.Sign( AngularVelocity );
		float rollingResistanceTorque = Fz * r * RollingResistance;
		float brakeAccel = (BrakeTorque + rollingResistanceTorque) * (sign * -1f) / Inertia;
		AngularVelocity += brakeAccel * deltaTime;

		// Prevent flip-flopping through zero
		if ( sign != MathF.Sign( AngularVelocity ) )
			AngularVelocity = 0f;

		// Visual roll angle
		RollAngle += MathX.RadianToDegree( AngularVelocity ) * deltaTime;
		RollAngle = (RollAngle % 360f + 360f) % 360f;
	}

	/// <summary>Returns the visual center of the wheel (hub position minus suspension travel, plus radius up).</summary>
	public Vector3 GetCenter()
	{
		var up = _rb.IsValid() ? _rb.WorldRotation.Up : Vector3.Up;
		return ContactPosition + up * Radius;
	}

	// ── Self-contained wheel model rendering ─────────────────────────────────
	/// <summary>
	/// Renders the wheel model — exact copy of lodzero's DrawTire().
	/// Position = wheel origin + Down * HitDistance.
	/// Rotation = Yaw(steer + flip) * Pitch(spin).
	/// </summary>
	protected override void OnPreRender()
	{
		if ( Model == null ) return;

		using ( Gizmo.Scope() )
		{
			// lodzero: float roll = IsRight ? -Tire.RollAngle : Tire.RollAngle;
			float roll = RenderFlipped ? -RollAngle : RollAngle;

			// lodzero: float yaw = (IsRight ? 180.0f : 0.0f) + Steering * Tire.SteeringRatio;
			float yaw = (RenderFlipped ? 180.0f : 0.0f) + SteerAngle * SteeringRatio;

			// lodzero: Vector3 offset = Tire.Offset + Vector3.Down * Tire.HitDistance;
			// Our Wheel is already at Tire.Offset position (it's a child GameObject),
			// so we just move down by HitDistance
			var renderTransform = Transform.World;
			renderTransform.Position += renderTransform.Down * HitDistance;

			// lodzero: Rotation localRot = world2.RotationToLocal( world2.Rotation );
			// localRot *= Rotation.FromYaw( yaw );
			// localRot *= Rotation.FromPitch( roll );
			// world2.Rotation = world2.RotationToWorld( localRot );
			Rotation localRot = renderTransform.RotationToLocal( renderTransform.Rotation );
			localRot *= Rotation.FromYaw( yaw );
			localRot *= Rotation.FromPitch( roll );
			renderTransform.Rotation = renderTransform.RotationToWorld( localRot );

			Gizmo.Draw.IgnoreDepth = false;
			Gizmo.Draw.Color = Color.White;
			var sceneObject = Gizmo.Draw.Model( Model, renderTransform );
			sceneObject.Flags.CastShadows = true;
			sceneObject.Tags.RemoveAll();
		}
	}

	// ── Runtime debug gizmos ──────────────────────────────────────────────────
	protected override void OnUpdate()
	{
		if ( ShowForceGizmos && _rb.IsValid() )
			DrawRuntimeGizmos();
	}

	private void DrawRuntimeGizmos()
	{
		var contact = ContactPosition;
		float visScale = 0.001f;

		// Suspension -- yellow
		var suspVec = _dbgSuspForce * visScale;
		DrawArrow( contact, suspVec, Color.Yellow );

		// Longitudinal -- green
		var longVec = _dbgLongForce * visScale;
		DrawArrow( contact, longVec, Color.Green );

		// Lateral -- blue
		var latVec = _dbgLatForce * visScale;
		DrawArrow( contact, latVec, Color.Blue );

		// Friction circle
		DebugOverlay.Text( contact + Vector3.Up * 20f,
			$"{GameObject.Name}  Grip={Grip:F0}",
			12f, TextFlag.None, Color.White, Time.Delta, false );
		DebugOverlay.Text( contact + Vector3.Up * 10f,
			$"Fz={Fz:F0} Fx={Fx:F0} Fy={Fy:F0} | SR={DynamicSlipRatio:F3} SA={DynamicSlipAngle:F2} w={AngularVelocity:F2}",
			11f, TextFlag.None, Color.White, Time.Delta, false );
		DebugOverlay.Text( contact,
			$"comp={Compression:F3} drv={DriveTorque:F1} brk={BrakeTorque:F1}",
			11f, TextFlag.None, Color.Gray, Time.Delta, false );
	}

	private void DrawArrow( Vector3 from, Vector3 vec, Color color )
	{
		if ( vec.LengthSquared < 0.001f ) return;
		var to = from + vec;
		var dir = vec.Normal;
		var perp = dir.Cross( Vector3.Up ).Normal;
		if ( perp.LengthSquared < 0.01f ) perp = dir.Cross( Vector3.Right ).Normal;
		float h = vec.Length * 0.2f;
		DebugOverlay.Line( from, to, color );
		DebugOverlay.Line( to, to - dir * h + perp * h * 0.5f, color );
		DebugOverlay.Line( to, to - dir * h - perp * h * 0.5f, color );
	}

	private float _logTimer;

	private void LogState()
	{
		if ( !LogToConsole ) return;

		_logTimer += Time.Delta;
		if ( _logTimer < 0.5f ) return;
		_logTimer = 0f;

		Log.Info( $"[{GameObject.Name}] Grip={Grip:F0} | w={AngularVelocity:F2} | Fz={Fz:F0} Fx={Fx:F0} Fy={Fy:F0} | SR={SlipRatio:F3} SA={SlipAngle:F2} | drv={DriveTorque:F1} brk={BrakeTorque:F1}" );
	}

	// ── Editor gizmos ─────────────────────────────────────────────────────────
	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;

		var endPos = Vector3.Down * LastLength;

		using ( Gizmo.Scope() )
		{
			Gizmo.Draw.LineThickness = 1f;
			Gizmo.Transform = new Transform();

			// Hub point and suspension line
			Gizmo.Draw.SolidSphere( WorldPosition, 1f );
			Gizmo.Draw.Line( WorldPosition, WorldPosition + WorldRotation * endPos );

			// Ghost cylinder showing tire shape
			var wheelCenter = WorldPosition + WorldRotation * endPos;
			var leftEdge = wheelCenter + WorldRotation.Left * Width * 0.5f;
			var rightEdge = wheelCenter + WorldRotation.Right * Width * 0.5f;

			Gizmo.Draw.Color = Color.White.WithAlpha( 0.1f );
			Gizmo.Draw.SolidCylinder( leftEdge, rightEdge, Radius, 16 );

			Gizmo.Draw.Color = Color.White;
			Gizmo.Draw.LineCylinder( leftEdge, rightEdge, Radius, Radius, 16 );
		}
	}
}
