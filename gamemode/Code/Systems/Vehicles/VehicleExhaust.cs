namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Controls exhaust smoke using a LegacyParticleSystem (cloud particle assets like facepunch/smoke001).
/// Scales the GameObject to control smoke thickness based on throttle.
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Exhaust" )]
[Icon( "air" )]
public sealed class VehicleExhaust : Component
{
	[Property, Group( "References" )] public Vehicle Vehicle { get; set; }

	/// <summary>GameObjects containing LegacyParticleSystem at exhaust positions.</summary>
	[Property, Group( "References" )] public List<GameObject> Exhausts { get; set; }

	/// <summary>Smoothing speed for throttle response.</summary>
	[Property, Group( "Settings" )] public float SmoothRate { get; set; } = 4f;

	/// <summary>Min particle scale when idle.</summary>
	[Property, Group( "Settings" )] public float IdleScale { get; set; } = 0.3f;

	/// <summary>Max particle scale at full throttle.</summary>
	[Property, Group( "Settings" )] public float FullThrottleScale { get; set; } = 1.5f;

	private float _smoothThrottle;

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() || Exhausts == null )
			return;

		bool hasDriver = Vehicle.Seats.Any( s => s.HasInput && s.Player.IsValid() );
		float targetThrottle = hasDriver ? Vehicle.DbgThrottle : 0f;

		_smoothThrottle = _smoothThrottle.LerpTo( targetThrottle, SmoothRate * Time.Delta );

		float scale = IdleScale.LerpTo( FullThrottleScale, _smoothThrottle );

		foreach ( var go in Exhausts )
		{
			if ( !go.IsValid() ) continue;
			go.Enabled = hasDriver || _smoothThrottle > 0.01f;
			go.LocalScale = Vector3.One * scale;
		}
	}
}
