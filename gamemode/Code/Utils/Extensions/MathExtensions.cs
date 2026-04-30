namespace OpenFramework.Utility;

public static class MathExtensions
{
	public static float MoveToLinear( this float from, float target, float speed )
	{
		var diff = target - from;
		var maxDelta = speed * Time.Delta;

		if ( Math.Abs( diff ) < maxDelta )
		{
			return target;
		}

		return from + Math.Sign( diff ) * maxDelta;
	}

	private const float InchesPerMeter = 39.3701f;

	/// <summary>Convert a Vector3 from inches to meters.</summary>
	public static Vector3 InchToMeterVector( this Vector3 v ) => v / InchesPerMeter;

	/// <summary>Convert a Vector3 from meters to inches.</summary>
	public static Vector3 MeterToInchVector( this Vector3 v ) => v * InchesPerMeter;

	/// <summary>
	/// Maps a value from range [a,b] to range [A,B] with clamping.
	/// Ported from lodzero's VehicleHelpers.MapRange.
	/// </summary>
	public static float MapRange( this float value, float a, float b, float A, float B )
	{
		return MathX.Clamp( A + ((value - a) * (B - A) / (b - a)), MathF.Min( A, B ), MathF.Max( A, B ) );
	}
}
