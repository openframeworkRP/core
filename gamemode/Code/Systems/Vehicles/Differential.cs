namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Ported from lodzero's Differential struct.
/// Splits input torque between left and right wheels based on differential type.
/// Also computes the input shaft velocity from wheel velocities.
/// </summary>
public struct Differential
{
	public DiffType Type;
	public float Ratio;

	/// <summary>Lock factor for LSD (0..1). Higher = more torque biasing toward the slower wheel.</summary>
	public float LsLockFactor;

	/// <summary>Torsen bias ratio. Limits how much more torque one side can receive vs the other.</summary>
	public float TorsenBias;

	public Differential( DiffType type, float ratio )
	{
		Type = type;
		Ratio = ratio;
		LsLockFactor = 0.5f;
		TorsenBias = 3.0f;
	}

	/// <summary>
	/// Splits input torque into left/right output torques based on differential type.
	/// Returns Vector2(leftTorque, rightTorque).
	/// </summary>
	public Vector2 GetOutputTorque( float inputTorque, float leftWheelRPM = 0, float rightWheelRPM = 0 )
	{
		switch ( Type )
		{
			case DiffType.Locked:
			{
				float lockedTorque = inputTorque * Ratio;
				return new Vector2( lockedTorque * 0.5f, lockedTorque * 0.5f );
			}

			case DiffType.Open:
			{
				float openTorque = inputTorque * Ratio;
				return new Vector2( openTorque * 0.5f, openTorque * 0.5f );
			}

			case DiffType.LSD:
			{
				float torque = inputTorque * Ratio;
				float slip = MathF.Abs( leftWheelRPM - rightWheelRPM );
				float bias = MathX.Clamp( 1.0f - slip * LsLockFactor, 0.5f, 1.0f );
				return new Vector2( torque * 0.5f * bias, torque * 0.5f * (2f - bias) );
			}

			case DiffType.Torsen:
			{
				float torsenTorque = inputTorque * Ratio;
				float speedRatio = rightWheelRPM == 0f ? 1f : leftWheelRPM / rightWheelRPM;
				speedRatio = MathX.Clamp( speedRatio, 1f / TorsenBias, TorsenBias );
				float leftTorque = torsenTorque * (speedRatio / (speedRatio + 1f));
				float rightTorque = torsenTorque - leftTorque;
				return new Vector2( leftTorque, rightTorque );
			}

			default:
			{
				float defTorque = inputTorque * Ratio;
				return new Vector2( defTorque * 0.5f, defTorque * 0.5f );
			}
		}
	}

	/// <summary>
	/// Computes the input shaft angular velocity from left/right wheel velocities.
	/// Simple average multiplied by the final drive ratio.
	/// </summary>
	public float GetInputShaftVelocity( float leftWheelVel, float rightWheelVel )
	{
		float avgVel = (leftWheelVel + rightWheelVel) * 0.5f;
		return avgVel * Ratio;
	}
}
