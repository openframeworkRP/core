using Sandbox;
using Sandbox.WeatherSystem;
using System;
using System.Collections.Generic;

/// <summary>
/// Système d'éclairage de ville dynamique.
///
/// SETUP :
///   1. Ajouter ce component sur un GameObject "CityLighting".
///   2. Assigner Weather (WeatherSystem).
///   3. Tagger chaque PointLight/SpotLight de ville avec le tag "city_light".
///   4. Configurer TurnOnHour / TurnOffHour.
///
/// Le fade est simulé en interpolant LightColor vers noir (éteint) ou
/// vers la couleur originale (allumé), sans toucher à Brightness.
/// </summary>
[Title( "City Lighting System" )]
[Category( "Environment" )]
[Icon( "lightbulb" )]
public sealed class CityLightingSystem : Component
{
	// ══════════════════════════════════════════
	//  RÉFÉRENCES
	// ══════════════════════════════════════════

	[Property, Group( "References" )]
	public WeatherSystem Weather { get; set; }

	// ══════════════════════════════════════════
	//  CONFIGURATION
	// ══════════════════════════════════════════

	[Property, Group( "Settings" ), Title( "Tag des lumières" )]
	public string LightTag { get; set; } = "city_light";

	[Property, Group( "Settings" )]
	[Range( 0f, 24f ), Title( "Heure allumage" )]
	public float TurnOnHour { get; set; } = 19.5f;

	[Property, Group( "Settings" )]
	[Range( 0f, 24f ), Title( "Heure extinction" )]
	public float TurnOffHour { get; set; } = 7.0f;

	[Property, Group( "Settings" )]
	[Range( 0f, 10f ), Title( "Durée fade (secondes réelles)" )]
	public float FadeTime { get; set; } = 3.0f;

	// ══════════════════════════════════════════
	//  DEBUG
	// ══════════════════════════════════════════

	[Property, Group( "Debug" ), ReadOnly]
	public float CurrentHour { get; private set; }

	[Property, Group( "Debug" ), ReadOnly]
	public bool LightsOn { get; private set; }

	[Property, Group( "Debug" ), ReadOnly]
	public int LightCount { get; private set; }

	// ══════════════════════════════════════════
	//  ÉTAT INTERNE
	// ══════════════════════════════════════════

	private struct LightEntry
	{
		public PointLight Light;
		public Color BaseColor;
	}

	private struct SpotEntry
	{
		public SpotLight Light;
		public Color BaseColor;
	}

	private List<LightEntry> _pointLights = new();
	private List<SpotEntry> _spotLights = new();
	private float _fadeAlpha = 0f;
	private float _fadeTarget = 0f;

	// ══════════════════════════════════════════
	//  LIFECYCLE
	// ══════════════════════════════════════════

	protected override void OnStart()
	{
		if ( !Weather.IsValid() )
			Weather = Scene.GetAllComponents<WeatherSystem>().FirstOrDefault();

		CollectLights();

		float h = GetCurrentHour();
		_fadeAlpha = ShouldBeOn( h ) ? 1f : 0f;
		_fadeTarget = _fadeAlpha;
		ApplyAlpha( _fadeAlpha );
	}

	protected override void OnUpdate()
	{
		if ( !Weather.IsValid() ) return;

		float h = GetCurrentHour();
		CurrentHour = h;
		_fadeTarget = ShouldBeOn( h ) ? 1f : 0f;

		if ( !_fadeAlpha.AlmostEqual( _fadeTarget, 0.001f ) )
		{
			float speed = FadeTime > 0f ? Time.Delta / FadeTime : 1f;
			_fadeAlpha = _fadeAlpha < _fadeTarget
				? MathF.Min( _fadeAlpha + speed, 1f )
				: MathF.Max( _fadeAlpha - speed, 0f );

			ApplyAlpha( _fadeAlpha );
		}

		LightsOn = _fadeTarget > 0.5f;
	}

	// ══════════════════════════════════════════
	//  LOGIQUE HORAIRE
	// ══════════════════════════════════════════

	private bool ShouldBeOn( float h )
	{
		if ( TurnOnHour > TurnOffHour )
			return h >= TurnOnHour || h < TurnOffHour;
		return h >= TurnOnHour && h < TurnOffHour;
	}

	private float GetCurrentHour()
	{
		if ( !Weather.IsValid() ) return 0f;
		float hours = Weather.Hour1 * 10f + Weather.Hour2;
		float minutes = Weather.Minute1 * 10f + Weather.Minute2;
		float secondFrac = (Time.Now % 1f) / 60f;
		return hours + (minutes + secondFrac) / 60f;
	}

	// ══════════════════════════════════════════
	//  COLLECTE DES LUMIÈRES
	// ══════════════════════════════════════════

	private void CollectLights()
	{
		_pointLights.Clear();
		_spotLights.Clear();

		foreach ( var go in Scene.GetAllObjects( true ) )
		{
			if ( !go.Tags.Has( LightTag ) ) continue;

			var point = go.Components.Get<PointLight>( FindMode.EverythingInSelf );
			if ( point != null )
			{
				_pointLights.Add( new LightEntry { Light = point, BaseColor = point.LightColor } );
				continue;
			}

			var spot = go.Components.Get<SpotLight>( FindMode.EverythingInSelf );
			if ( spot != null )
				_spotLights.Add( new SpotEntry { Light = spot, BaseColor = spot.LightColor } );
		}

		LightCount = _pointLights.Count + _spotLights.Count;
		Log.Info( $"[CityLighting] {_pointLights.Count} PointLights + {_spotLights.Count} SpotLights trouvées (tag: '{LightTag}')" );
	}

	// ══════════════════════════════════════════
	//  APPLICATION DU FADE
	//  On interpole LightColor → noir (éteint) ou couleur originale (allumé)
	// ══════════════════════════════════════════

	private void ApplyAlpha( float alpha )
	{
		float eased = SmoothStep( alpha );

		foreach ( var entry in _pointLights )
		{
			if ( !entry.Light.IsValid() ) continue;
			entry.Light.LightColor = Color.Lerp( Color.Black, entry.BaseColor, eased );
		}

		foreach ( var entry in _spotLights )
		{
			if ( !entry.Light.IsValid() ) continue;
			entry.Light.LightColor = Color.Lerp( Color.Black, entry.BaseColor, eased );
		}
	}

	// ══════════════════════════════════════════
	//  API PUBLIQUE
	// ══════════════════════════════════════════

	/// <summary>Recharge les lumières après un spawn dynamique.</summary>
	public void RefreshLights() => CollectLights();

	/// <summary>Force l'état sans fade.</summary>
	public void ForceState( bool on )
	{
		_fadeAlpha = on ? 1f : 0f;
		_fadeTarget = _fadeAlpha;
		ApplyAlpha( _fadeAlpha );
	}

	private static float SmoothStep( float t ) => t * t * (3f - 2f * t);
}
