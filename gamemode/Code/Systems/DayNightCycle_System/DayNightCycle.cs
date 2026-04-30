using Sandbox;
using System;
using Sandbox.WeatherSystem;

/// <summary>
/// Cycle jour/nuit coordonné avec WeatherSystem.
/// Le WeatherSystem reste maître du temps (Hour1/Hour2/Minute1/Minute2).
/// Ce component lit l'heure depuis WeatherSystem et applique les couleurs.
///
/// Setup :
///   1. Assigner SunLight (DirectionalLight).
///   2. Assigner Weather (référence au WeatherSystem de la scène).
///   3. Supprimer l'appel à UpdateSunRotation() dans WeatherSystem.OnUpdate()
///      — ce component s'en charge.
/// </summary>
[Title( "Day Night Cycle" )]
[Category( "Environment" )]
[Icon( "wb_sunny" )]
public sealed class DayNightCycle : Component
{
	// ══════════════════════════════════════════
	//  RÉFÉRENCES
	// ══════════════════════════════════════════

	[Property, Group( "References" )]
	public DirectionalLight SunLight { get; set; }

	/// <summary>Référence au WeatherSystem qui gère l'horloge.</summary>
	[Property, Group( "References" )]
	public WeatherSystem Weather { get; set; }

	// ══════════════════════════════════════════
	//  ROTATION
	// ══════════════════════════════════════════

	/// <summary>Heure du lever du soleil.</summary>
	[Property, Group( "Rotation" )]
	[Range( 0f, 12f ), Title( "Heure lever du soleil" )]
	public float SunriseHour { get; set; } = 6f;

	/// <summary>Heure du coucher du soleil.</summary>
	[Property, Group( "Rotation" )]
	[Range( 12f, 24f ), Title( "Heure coucher du soleil" )]
	public float SunsetHour { get; set; } = 20f;

	/// <summary>
	/// Durée de la transition jour ↔ nuit en heures de jeu.
	/// Ex : 1.5 = la nuit s'installe progressivement sur 1h30 après le coucher.
	/// </summary>
	[Property, Group( "Rotation" )]
	[Range( 0.1f, 10f ), Title( "Durée transition jour/nuit (heures)" )]
	public float TransitionDuration { get; set; } = 1.5f;

	/// <summary>Décalage nord/sud de l'arc (0° = équateur, positif = vers le sud).</summary>
	[Property, Group( "Rotation" )]
	[Range( -45f, 45f ), Title( "Inclinaison N/S de l'arc" )]
	public float SunTilt { get; set; } = 20f;

	// ══════════════════════════════════════════
	//  BROUILLARD
	// ══════════════════════════════════════════

	[Property, Group( "Fog" )] public bool ControlFog { get; set; } = false;
	[Property, Group( "Fog" ), Range( 0f, 1f )] public float FogNight { get; set; } = 0.35f;
	[Property, Group( "Fog" ), Range( 0f, 1f )] public float FogDay { get; set; } = 0.05f;
	[Property, Group( "Fog" ), Range( 0f, 1f )] public float FogTransition { get; set; } = 0.20f;

	// ══════════════════════════════════════════
	//  SKY COLOR SOLEIL
	// ══════════════════════════════════════════

	[Property, Group( "Sky Colors – Day" )]
	[Range( 0f, 0.5f ), Title( "Luminosité min (plancher SkyColor)" )]
	public float MinSkyBrightness { get; set; } = 0.10f;

	// Ce shader fait : couleur_finale = atmosphere_physique × SkyColor
	// L'atmosphere physique est DÉJÀ colorée (bleu le jour, orange au lever).
	// SkyColor doit donc rester PROCHE DU BLANC pour ne pas sur-saturer.
	// Légère teinte = ajustement doux, pas transformation complète.
	[Property, Group( "Sky Colors – Day" )] public Color SkyDawn { get; set; } = new Color( 1.00f, 0.85f, 0.70f ); // légèrement chaud
	[Property, Group( "Sky Colors – Day" )] public Color SkySunrise { get; set; } = new Color( 1.00f, 0.88f, 0.72f ); // blanc doré
	[Property, Group( "Sky Colors – Day" )] public Color SkyMorning { get; set; } = new Color( 0.90f, 0.95f, 1.00f ); // blanc bleuté
	[Property, Group( "Sky Colors – Day" )] public Color SkyNoon { get; set; } = new Color( 0.85f, 0.92f, 1.00f ); // blanc bleu clair
	[Property, Group( "Sky Colors – Day" )] public Color SkyAfternoon { get; set; } = new Color( 0.88f, 0.93f, 1.00f ); // blanc azur
	[Property, Group( "Sky Colors – Day" )] public Color SkySunset { get; set; } = new Color( 1.00f, 0.82f, 0.62f ); // blanc orangé
	[Property, Group( "Sky Colors – Day" )] public Color SkyDusk { get; set; } = new Color( 0.80f, 0.72f, 0.90f ); // blanc mauve

	// ══════════════════════════════════════════
	//  SKY COLOR / LIGHT COLOR LUNE
	// ══════════════════════════════════════════

	[Property, Group( "Sky Colors – Night" )]
	public Color SkyMoon { get; set; } = new Color( 0.70f, 0.75f, 0.90f ); // blanc bleuté nuit

	// LightColor = teinte de la lumière sur les objets.
	// Doit être CLAIRE (valeur haute) et PEU SATURÉE (vers le centre du picker).
	// Curseur toujours dans la moitié SUPÉRIEURE du color picker.
	[Property, Group( "Light Colors – Day" )] public Color LightDawn { get; set; } = new Color( 1.00f, 0.82f, 0.68f ); // #FFD1AD — blanc rosé
	[Property, Group( "Light Colors – Day" )] public Color LightSunrise { get; set; } = new Color( 1.00f, 0.88f, 0.65f ); // #FFE0A6 — blanc doré
	[Property, Group( "Light Colors – Day" )] public Color LightMorning { get; set; } = new Color( 1.00f, 0.96f, 0.84f ); // #FFF5D6 — blanc chaud
	[Property, Group( "Light Colors – Day" )] public Color LightNoon { get; set; } = new Color( 1.00f, 0.98f, 0.96f ); // #FFFAF5 — blanc neutre
	[Property, Group( "Light Colors – Day" )] public Color LightAfternoon { get; set; } = new Color( 1.00f, 0.96f, 0.86f ); // #FFF5DB — blanc légèrement chaud
	[Property, Group( "Light Colors – Day" )] public Color LightSunset { get; set; } = new Color( 1.00f, 0.78f, 0.50f ); // #FFC880 — or pâle
	[Property, Group( "Light Colors – Day" )] public Color LightDusk { get; set; } = new Color( 0.85f, 0.72f, 0.88f ); // #D8B8E0 — mauve clair

	[Property, Group( "Light Colors – Night" )]
	public Color LightMoon { get; set; } = new Color( 0.03f, 0.04f, 0.08f );

	// ══════════════════════════════════════════
	//  HEURE COURANTE (lecture seule, sync depuis WeatherSystem)
	// ══════════════════════════════════════════

	[Property, Group( "Time" ), ReadOnly]
	public float CurrentHour { get; private set; }

	// ══════════════════════════════════════════
	//  ÉVÉNEMENT
	// ══════════════════════════════════════════

	public delegate void PhaseChangedDelegate( DayPhase phase );
	public event PhaseChangedDelegate OnPhaseChanged;

	public enum DayPhase { None, Night, Dawn, Sunrise, Morning, Noon, Afternoon, Sunset, Dusk, Evening }

	private DayPhase _phase = DayPhase.None;

	// ══════════════════════════════════════════
	//  LIFECYCLE
	// ══════════════════════════════════════════

	protected override void OnStart()
	{
		// Auto-find WeatherSystem si non assigné dans l'éditeur
		if ( !Weather.IsValid() )
			Weather = Scene.GetAllComponents<WeatherSystem>().FirstOrDefault();

		if ( !Weather.IsValid() )
			Log.Warning( "[DayNightCycle] WeatherSystem introuvable — assigne-le dans l'éditeur." );
		else
			Log.Info( $"[DayNightCycle] OnStart — Weather hour = {Weather.Hour1}{Weather.Hour2}:{Weather.Minute1}{Weather.Minute2}" );

		ApplyAll();
	}

	protected override void OnUpdate()
	{
		// Tourne sur TOUS les clients — les valeurs Hour*/Minute* sont syncées
		ApplyAll();
		CheckPhase();
	}

	// ══════════════════════════════════════════
	//  APPLICATION CENTRALE
	// ══════════════════════════════════════════

	private void ApplyAll()
	{
		if ( !SunLight.IsValid() ) return;
		if ( !Weather.IsValid() ) return;

		// ── Heure courante ────────────────────────────────────────────
		float hours = Weather.Hour1 * 10f + Weather.Hour2;
		float minutes = Weather.Minute1 * 10f + Weather.Minute2;
		float secondFrac = (Time.Now % 1f) / 60f;
		float h = hours + (minutes + secondFrac) / 60f;
		CurrentHour = h;

		float dayDuration = SunsetHour - SunriseHour;
		float nightDuration = 24f - dayDuration;

		// ── Pitch ─────────────────────────────────────────────────────
		// Basé uniquement sur h, jamais sur isDay.
		float rawPitch;
		if ( h >= SunriseHour && h <= SunsetHour )
		{
			float t = (h - SunriseHour) / dayDuration;
			rawPitch = MathF.Sin( t * MathF.PI ) * 90f;
		}
		else
		{
			float hNight = h >= SunsetHour ? h - SunsetHour : h + (24f - SunsetHour);
			float t = hNight / nightDuration;
			rawPitch = -MathF.Sin( t * MathF.PI ) * 90f;
		}
		float pitch = MathF.Abs( rawPitch );

		// ── Yaw ───────────────────────────────────────────────────────
		// Basé uniquement sur h — pas de switch brutal isDay/!isDay.
		float sunYaw;
		if ( h >= SunriseHour && h <= SunsetHour )
		{
			// Jour : est (90°) → ouest (270°)
			float t = (h - SunriseHour) / dayDuration;
			sunYaw = 90f + t * 180f;
		}
		else
		{
			// Nuit : ouest (270°) → est (90°) = 270° → 450°
			float hNight = h >= SunsetHour ? h - SunsetHour : h + (24f - SunsetHour);
			float t = hNight / nightDuration;
			sunYaw = 270f + t * 180f;
		}

		SunLight.GameObject.LocalRotation = Rotation.From( pitch, sunYaw, SunTilt );
		SunLight.WorldRotation = Rotation.From( pitch, sunYaw, SunTilt );

		// ── Couleurs — basées sur h, transition progressive ───────────
		// dayBlend = 1 (plein jour) → 0 (pleine nuit)
		// La transition commence TransitionDuration heures AVANT le coucher
		// et se termine TransitionDuration heures APRÈS le lever.
		float dayBlend = SampleDayNightBlend( h );

		Color skyDay = ApplyBrightnessFloor( SampleSkyColor( h ) );
		Color lightDay = SampleLightColor( h );

		SunLight.SkyColor = Color.Lerp( SkyMoon, skyDay, dayBlend );
		SunLight.LightColor = Color.Lerp( LightMoon, lightDay, dayBlend );

		if ( ControlFog )
			SunLight.FogStrength = SampleFog( h );
	}

	// ══════════════════════════════════════════
	//  PLANCHER SKYCOLOR
	// ══════════════════════════════════════════

	private Color ApplyBrightnessFloor( Color c )
	{
		float brightest = MathF.Max( c.r, MathF.Max( c.g, c.b ) );
		if ( brightest <= 0f )
			return new Color( MinSkyBrightness, MinSkyBrightness, MinSkyBrightness, c.a );
		if ( brightest < MinSkyBrightness )
		{
			float scale = MinSkyBrightness / brightest;
			return new Color( c.r * scale, c.g * scale, c.b * scale, c.a );
		}
		return c;
	}

	// ══════════════════════════════════════════
	//  KEYFRAMES — SKY COLOR
	// ══════════════════════════════════════════

	private Color SampleSkyColor( float h )
	{
		(float t, Color c)[] keys =
		{
			(  0f, SkyDawn      ),
			(  5f, SkyDawn      ),
			(  6f, SkySunrise   ),
			(  8f, SkyMorning   ),
			( 12f, SkyNoon      ),
			( 16f, SkyAfternoon ),
			( 18f, SkySunset    ),
			( 20f, SkyDusk      ),
			( 24f, SkyDusk      ),
		};
		return SampleKeyframes( h, keys );
	}

	// ══════════════════════════════════════════
	//  KEYFRAMES — LIGHT COLOR
	// ══════════════════════════════════════════

	private Color SampleLightColor( float h )
	{
		(float t, Color c)[] keys =
		{
			(  0f, LightDawn      ),
			(  5f, LightDawn      ),
			(  6f, LightSunrise   ),
			(  8f, LightMorning   ),
			( 12f, LightNoon      ),
			( 16f, LightAfternoon ),
			( 18f, LightSunset    ),
			( 20f, LightDusk      ),
			( 24f, LightDusk      ),
		};
		return SampleKeyframes( h, keys );
	}

	// ══════════════════════════════════════════
	//  KEYFRAMES — FOG
	// ══════════════════════════════════════════

	private float SampleFog( float h )
	{
		(float t, float v)[] keys =
		{
			(  0f, FogNight      ),
			(  4f, FogNight      ),
			(  6f, FogTransition ),
			(  8f, FogDay        ),
			( 18f, FogDay        ),
			( 19f, FogTransition ),
			( 21f, FogNight      ),
			( 24f, FogNight      ),
		};
		return SampleKeyframes( h, keys );
	}

	// ══════════════════════════════════════════
	//  DÉTECTION DE PHASE
	// ══════════════════════════════════════════

	private void CheckPhase()
	{
		var p = GetPhase( CurrentHour );
		if ( p == _phase ) return;
		_phase = p;
		Log.Info( $"[DayNightCycle] {p}  ({CurrentHour:F1}h)" );
		OnPhaseChanged?.Invoke( p );
	}

	public static DayPhase GetPhase( float h )
	{
		if ( h < 4f ) return DayPhase.Night;
		if ( h < 6f ) return DayPhase.Dawn;
		if ( h < 8f ) return DayPhase.Sunrise;
		if ( h < 11f ) return DayPhase.Morning;
		if ( h < 13f ) return DayPhase.Noon;
		if ( h < 17f ) return DayPhase.Afternoon;
		if ( h < 19f ) return DayPhase.Sunset;
		if ( h < 21f ) return DayPhase.Dusk;
		return DayPhase.Evening;
	}

	// ══════════════════════════════════════════
	//  API PUBLIQUE
	// ══════════════════════════════════════════

	public DayPhase Phase => _phase;
	public bool IsDay => CurrentHour >= 6f && CurrentHour < 20f;

	// ══════════════════════════════════════════
	//  BLEND JOUR / NUIT
	//  Retourne 1 = plein jour, 0 = pleine nuit
	//  Transition progressive autour du lever et du coucher
	// ══════════════════════════════════════════

	private float SampleDayNightBlend( float h )
	{
		// Transition COUCHER : commence TransitionDuration avant SunsetHour
		// Ex: SunsetHour=20, Duration=1.5 → transition de 18h30 à 20h00
		float sunsetStart = SunsetHour - TransitionDuration;
		if ( h >= sunsetStart && h <= SunsetHour )
		{
			float t = (h - sunsetStart) / TransitionDuration;
			return SmoothStep( Math.Clamp( 1f - t, 0f, 1f ) );
		}

		// Transition LEVER : se termine TransitionDuration après SunriseHour
		// Ex: SunriseHour=6, Duration=1.5 → transition de 6h00 à 7h30
		float sunriseEnd = SunriseHour + TransitionDuration;
		if ( h >= SunriseHour && h <= sunriseEnd )
		{
			float t = (h - SunriseHour) / TransitionDuration;
			return SmoothStep( Math.Clamp( t, 0f, 1f ) );
		}

		// Plein jour
		if ( h > SunriseHour && h < SunsetHour )
			return 1f;

		// Pleine nuit
		return 0f;
	}

	private static Color SampleKeyframes( float h, (float t, Color c)[] keys )
	{
		if ( h <= keys[0].t ) return keys[0].c;
		if ( h >= keys[^1].t ) return keys[^1].c;
		for ( int i = 0; i < keys.Length - 1; i++ )
		{
			if ( h < keys[i + 1].t )
			{
				float a = SmoothStep( (h - keys[i].t) / (keys[i + 1].t - keys[i].t) );
				return Color.Lerp( keys[i].c, keys[i + 1].c, a );
			}
		}
		return keys[^1].c;
	}

	private static float SampleKeyframes( float h, (float t, float v)[] keys )
	{
		if ( h <= keys[0].t ) return keys[0].v;
		if ( h >= keys[^1].t ) return keys[^1].v;
		for ( int i = 0; i < keys.Length - 1; i++ )
		{
			if ( h < keys[i + 1].t )
			{
				float a = SmoothStep( (h - keys[i].t) / (keys[i + 1].t - keys[i].t) );
				return MathX.Lerp( keys[i].v, keys[i + 1].v, a );
			}
		}
		return keys[^1].v;
	}

	private static float SmoothStep( float t ) => t * t * (3f - 2f * t);
}
