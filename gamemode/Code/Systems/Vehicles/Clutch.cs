using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Ported from lodzero's Clutch struct.
/// Simulates a friction clutch between the engine and gearbox.
/// Transfers torque based on slip between engine and clutch-side angular velocities,
/// with automatic engagement based on RPM and slip conditions.
/// </summary>
public struct Clutch
{
	/// <summary>Angular velocity of the clutch output (from wheels/gearbox side).</summary>
	public float ClutchAngVelocity;

	/// <summary>Angular velocity of the engine crankshaft.</summary>
	public float EngineAngVelocity;

	/// <summary>Current gearbox ratio being used.</summary>
	public float GearboxRatio;

	/// <summary>How locked the clutch is (0 = fully disengaged, 1 = fully engaged).</summary>
	public float ClutchLock;

	/// <summary>Target clutch lock value before smoothing.</summary>
	public float DesiredClutchLock;

	/// <summary>Output torque transmitted through the clutch.</summary>
	public float ClutchTorque;

	/// <summary>Maximum torque the clutch can transfer before slipping.</summary>
	public float ClutchMaxTorque;

	/// <summary>Maximum torque the engine can produce (used to calculate ClutchMaxTorque).</summary>
	public float EngineMaxTorque;

	/// <summary>Dampening factor for torque smoothing (0..1). Higher = smoother transitions.</summary>
	public float ClutchDamping;

	/// <summary>Clutch spring strength — how aggressively it transfers torque based on slip.</summary>
	public float ClutchStiffness;

	/// <summary>Torque capacity multiplier over engine max torque.</summary>
	public float ClutchCapacity;

	private static readonly float RadToRPM = 60.0f / (MathF.PI * 2.0f);

	public Clutch( float maxTorque, float capacity, float stiffness, float damping )
	{
		EngineMaxTorque = maxTorque;
		ClutchCapacity = capacity;
		ClutchStiffness = stiffness;
		ClutchDamping = damping;

		ClutchMaxTorque = EngineMaxTorque * ClutchCapacity;
		ClutchTorque = 0.0f;
		ClutchLock = 0.0f;
		DesiredClutchLock = 0.0f;
		ClutchAngVelocity = 0.0f;
		EngineAngVelocity = 0.0f;
		GearboxRatio = 0.0f;
	}

	/// <summary>
	/// Updates the clutch state each physics tick.
	/// </summary>
	public void Update( float deltaTime, float outputShaftVelocity, float engineAngularVelocity, float gearboxRatio, float throttle, float rpm, bool isShifting )
	{
		ClutchAngVelocity = outputShaftVelocity;
		EngineAngVelocity = engineAngularVelocity;
		GearboxRatio = gearboxRatio;

		UpdateClutchTorque( deltaTime, throttle, rpm, isShifting );
	}

	private void UpdateClutchTorque( float deltaTime, float throttle, float rpm, bool forceDisengage )
	{
		// Calculate slip between engine and clutch side
		float slip = (EngineAngVelocity - ClutchAngVelocity) * MathF.Sign( MathF.Abs( GearboxRatio ) );

		// Convert angular values to RPM for easier interpretation
		float engineRPM = EngineAngVelocity * RadToRPM;
		float slipRPM = slip * RadToRPM;

		// Lock based on engine RPM (engages more as RPM increases)
		float rpmLock = engineRPM.MapRange( 900.0f, 2000.0f, 0.0f, 1.0f );

		// Lock based on slip RPM (engages clutch even at low RPM when there's difference)
		float slipLock = MathX.Clamp( slipRPM.MapRange( 0.0f, 500.0f, 0.0f, 1.0f ), 0.0f, 1.0f );

		// Fully locked if gearbox is in neutral
		float lockWhenNeutral = (GearboxRatio == 0.0f) ? 1.0f : 0.0f;

		// Final clutch lock value (maximum of all conditions)
		float maxLock = MathF.Max( lockWhenNeutral, MathF.Max( rpmLock, slipLock ) );
		DesiredClutchLock = MathX.Clamp( maxLock, 0.0f, 1.0f );

		// Disable clutch lock if both engine and clutch are near idle RPM and no throttle,
		// or if the gearbox is mid-shift
		bool belowIdle = EngineAngVelocity < 900.0f * (MathF.PI * 2.0f / 60.0f) + 0.1f;
		bool clutchStationary = MathF.Abs( ClutchAngVelocity ) < 100.0f;

		if ( (belowIdle && clutchStationary) || forceDisengage )
		{
			DesiredClutchLock = 0.0f;
			ClutchTorque = 0.0f;
		}

		ClutchLock = MathX.Lerp( ClutchLock, DesiredClutchLock, deltaTime * 10.0f );

		// Target torque is proportional to slip and clutch lock
		float targetTorque = slip * ClutchLock * ClutchStiffness;

		// Suppress very small torque to prevent creep
		if ( MathF.Abs( targetTorque ) < 1.0f )
			targetTorque = 0.0f;

		// Clamp torque to clutch capacity
		targetTorque = MathX.Clamp( targetTorque, -ClutchMaxTorque, ClutchMaxTorque );

		// Smooth output with damping (softens sudden torque changes)
		ClutchTorque = targetTorque + ((ClutchTorque - targetTorque) * ClutchDamping);

		// Kill clutch torque at idle with no throttle
		if ( throttle == 0.0f && rpm <= 905.0f )
		{
			ClutchTorque = 0.0f;
		}
	}
}
