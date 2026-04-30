using Sandbox.Audio;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// A single engine sound layer config that covers a specific RPM range.
/// </summary>
public class EngineSoundLayer
{
	/// <summary>Engine loop under load (throttle applied).</summary>
	[Property] public SoundEvent OnClip { get; set; }

	/// <summary>Engine loop off-throttle (deceleration / coasting).</summary>
	[Property] public SoundEvent OffClip { get; set; }

	/// <summary>Layer display name (for debugging).</summary>
	[Property] public string Name { get; set; } = "Unnamed";

	/// <summary>RPM where this layer starts fading in (volume 0→1 from A→B).</summary>
	[Property, Group( "RPM Range" )] public float A { get; set; } = 0f;

	/// <summary>RPM where this layer reaches full volume.</summary>
	[Property, Group( "RPM Range" )] public float B { get; set; } = 2000f;

	/// <summary>RPM where this layer starts fading out (volume 1→0 from C→D).</summary>
	[Property, Group( "RPM Range" )] public float C { get; set; } = 4000f;

	/// <summary>RPM where this layer is fully silent.</summary>
	[Property, Group( "RPM Range" )] public float D { get; set; } = 6000f;

	/// <summary>Volume multiplier for this layer.</summary>
	[Property, Group( "Tuning" )] public float VolumeMultiplier { get; set; } = 1.0f;

	/// <summary>Semitone range for pitch calculation. 12 = one octave.</summary>
	[Property, Group( "Tuning" )] public float SemiTone { get; set; } = 12.0f;

	/// <summary>Additional pitch multiplier.</summary>
	[Property, Group( "Tuning" )] public float PitchMultiplier { get; set; } = 1.0f;

	/// <summary>Volume multiplier for the off-throttle clip relative to on-throttle.</summary>
	[Property, Group( "Tuning" )] public float OffVolumeScale { get; set; } = 0.15f;
}

/// <summary>
/// Runtime state for a single engine sound layer (not serialized).
/// </summary>
internal class LayerRuntime
{
	public SoundHandle OnHandle;
	public SoundHandle OffHandle;

	public void Play( EngineSoundLayer config, Vector3 position )
	{
		if ( config.OnClip != null )
			OnHandle = Sound.Play( config.OnClip, position );
		if ( config.OffClip != null )
			OffHandle = Sound.Play( config.OffClip, position );
	}

	public void Stop()
	{
		OnHandle?.Stop();
		OnHandle = null;
		OffHandle?.Stop();
		OffHandle = null;
	}

	public void Update( EngineSoundLayer config, Vector3 position, float rpm, float load )
	{
		// ── Restart sounds that have stopped (looping fallback) ───────
		if ( config.OnClip != null && (OnHandle == null || OnHandle.IsStopped) )
			OnHandle = Sound.Play( config.OnClip, position );
		if ( config.OffClip != null && (OffHandle == null || OffHandle.IsStopped) )
			OffHandle = Sound.Play( config.OffClip, position );

		// ── Volume: fade in A→B, full B→C, fade out C→D ──────────────
		float vol;
		if ( rpm <= config.B )
			vol = rpm.MapRange( config.A, config.B, 0f, 1f );
		else
			vol = rpm.MapRange( config.C, config.D, 1f, 0f );

		vol = MathX.Clamp( vol, 0f, 1f );
		vol = SmoothStep( vol );

		// ── Load blend: on-throttle vs off-throttle ──────────────────
		float loadClamped = MathX.Clamp( load, -1f, 1f );
		float onWeight = MathX.Clamp( (loadClamped + 1f) * 0.5f, 0f, 1f );
		float offWeight = 1f - onWeight;

		// ── Pitch from RPM (semitone-based) ──────────────────────────
		float pitch = RpmToPitch( config, rpm );

		if ( OnHandle != null )
		{
			OnHandle.Volume = vol * onWeight * config.VolumeMultiplier;
			OnHandle.Position = position;
			OnHandle.Pitch = pitch;
		}

		if ( OffHandle != null )
		{
			OffHandle.Volume = vol * offWeight * config.VolumeMultiplier * config.OffVolumeScale;
			OffHandle.Position = position;
			OffHandle.Pitch = pitch;
		}
	}

	private static float RpmToPitch( EngineSoundLayer config, float rpm )
	{
		float range = config.D - config.A;
		if ( range <= 0f ) return 1f;
		float normalized = MathX.Clamp( (rpm - config.A) / range, 0f, 1f );
		float semitones = -config.SemiTone + normalized * config.SemiTone * 2f;
		return MathF.Pow( 2f, semitones / config.SemiTone ) * config.PitchMultiplier;
	}

	private static float SmoothStep( float x )
	{
		x = MathX.Clamp( x, 0f, 1f );
		return x * x * (3f - 2f * x);
	}
}

/// <summary>
/// Vehicle audio system based on lodzero's layered engine sound approach.
/// Multiple SoundLayers crossfade based on RPM, with on/off throttle blending.
/// Also handles tire screech based on slip intensity.
/// </summary>
[Category( "Vehicles" )]
[Title( "Vehicle Sound" )]
[Icon( "volume_up" )]
public sealed class VehicleSound : Component
{
	[Property] public Vehicle Vehicle { get; set; }

	/// <summary>List of engine sound layers covering different RPM ranges.</summary>
	[Property, Group( "Engine" )] public List<EngineSoundLayer> Layers { get; set; } = new();

	/// <summary>Engine start sound (played once when driver enters).</summary>
	[Property, Group( "Engine" )] public SoundEvent EngineStart { get; set; }

	/// <summary>Engine stop sound (played once when driver exits).</summary>
	[Property, Group( "Engine" )] public SoundEvent EngineStop { get; set; }

	/// <summary>Master volume for all engine layers (0-1).</summary>
	[Property, Group( "Engine" )] public float MasterVolume { get; set; } = 0.75f;

	/// <summary>Tire screech/rolling loop for asphalt/default surfaces.</summary>
	[Property, Group( "Tires" )] public SoundEvent TireAsphaltLoop { get; set; }

	/// <summary>Tire loop for grass/dirt surfaces.</summary>
	[Property, Group( "Tires" )] public SoundEvent TireGrassLoop { get; set; }

	/// <summary>Tire loop for sand/gravel surfaces.</summary>
	[Property, Group( "Tires" )] public SoundEvent TireSandLoop { get; set; }

	/// <summary>Maximum screech volume (0-1).</summary>
	[Property, Group( "Tires" )] public float ScreechMaxVolume { get; set; } = 1.0f;

	/// <summary>Rolling sound volume at full speed (0-1). Plays even without skidding.</summary>
	[Property, Group( "Tires" )] public float RollingMaxVolume { get; set; } = 0.4f;

	/// <summary>Speed (km/h) at which rolling sound reaches full volume.</summary>
	[Property, Group( "Tires" )] public float RollingFullSpeedKmh { get; set; } = 80f;

	/// <summary>Sound played repeatedly when fuel is in reserve.</summary>
	[Property, Group( "Fuel" )] public SoundEvent FuelWarningBeep { get; set; }

	/// <summary>Interval between fuel warning beeps (seconds).</summary>
	[Property, Group( "Fuel" )] public float FuelWarningInterval { get; set; } = 3f;

	/// <summary>Enable detailed audio debug logs in the console.</summary>
	[Property, Group( "Debug" )] public bool ShowDebugLogs { get; set; } = false;

	// ── Runtime state ─────────────────────────────────────────────────────────

	private List<LayerRuntime> _runtimes = new();
	private bool _engineRunning;
	private float _smoothSkid;
	private Mixer _mixer;
	private TimeSince _debugLogTimer;
	private float _fuelWarningTimer;

	// ── Surface sound state ───────────────────────────────────────────────
	private SoundHandle _asphaltHandle;
	private SoundHandle _grassHandle;
	private SoundHandle _sandHandle;
	private float _asphaltWeight;
	private float _grassWeight;
	private float _sandWeight;

	protected override void OnStart()
	{
		_mixer = Mixer.FindMixerByName( "Vehicle" );
		if ( _mixer == null )
		{
			_mixer = Mixer.Master.AddChild();
			_mixer.Name = "Vehicle";
		}
		_mixer.Volume = MasterVolume;
	}

	protected override void OnUpdate()
	{
		if ( !Vehicle.IsValid() )
			return;

		bool hasDriver = Vehicle.Seats.Any( s => s.HasInput && s.Player.IsValid() );

		if ( hasDriver && !_engineRunning )
			StartEngine();
		else if ( !hasDriver && _engineRunning )
			StopEngine();

		if ( _engineRunning )
			UpdateEngineLayers();

		UpdateTireSounds();
		UpdateFuelWarning();

		// Debug logging every 1 second
		if ( ShowDebugLogs && _debugLogTimer > 1f )
		{
			_debugLogTimer = 0;
			float rpm = Vehicle.Engine.IsValid() ? Vehicle.Engine.RPM : -1f;
			float throttle = Vehicle.DbgThrottle;
			Log.Info( $"[VehicleSound] engineRunning={_engineRunning} hasDriver={hasDriver} RPM={rpm:F0} Throttle={throttle:F2} Layers={Layers.Count} Runtimes={_runtimes.Count}" );

			for ( int i = 0; i < _runtimes.Count && i < Layers.Count; i++ )
			{
				var rt = _runtimes[i];
				var cfg = Layers[i];
				bool onValid = rt.OnHandle != null && !rt.OnHandle.IsStopped;
				bool offValid = rt.OffHandle != null && !rt.OffHandle.IsStopped;
				float onVol = rt.OnHandle?.Volume ?? -1f;
				float offVol = rt.OffHandle?.Volume ?? -1f;
				float onPitch = rt.OnHandle?.Pitch ?? -1f;
				Log.Info( $"  [{cfg.Name}] range={cfg.A}-{cfg.B}-{cfg.C}-{cfg.D} OnPlaying={onValid}(vol={onVol:F3}) OffPlaying={offValid}(vol={offVol:F3}) pitch={onPitch:F3}" );
			}
		}
	}

	private void StartEngine()
	{
		_engineRunning = true;

		if ( ShowDebugLogs )
			Log.Info( $"[VehicleSound] StartEngine: EngineStart={EngineStart != null} Layers={Layers.Count}" );

		if ( EngineStart != null )
			Sound.Play( EngineStart, WorldPosition );

		// Create runtime handles for each layer
		_runtimes.Clear();
		foreach ( var layer in Layers )
		{
			var rt = new LayerRuntime();
			rt.Play( layer, WorldPosition );

			if ( ShowDebugLogs )
				Log.Info( $"[VehicleSound] Layer '{layer.Name}': OnClip={layer.OnClip != null} OffClip={layer.OffClip != null} OnHandle={rt.OnHandle != null} OffHandle={rt.OffHandle != null}" );

			if ( rt.OnHandle != null && _mixer != null )
				rt.OnHandle.TargetMixer = _mixer;
			if ( rt.OffHandle != null && _mixer != null )
				rt.OffHandle.TargetMixer = _mixer;

			_runtimes.Add( rt );
		}
	}

	private void StopEngine()
	{
		_engineRunning = false;

		foreach ( var rt in _runtimes )
			rt.Stop();
		_runtimes.Clear();

		_asphaltHandle?.Stop();
		_asphaltHandle = null;
		_grassHandle?.Stop();
		_grassHandle = null;
		_sandHandle?.Stop();
		_sandHandle = null;

		if ( EngineStop != null )
			Sound.Play( EngineStop, WorldPosition );
	}

	private void UpdateEngineLayers()
	{
		if ( !Vehicle.Engine.IsValid() )
			return;

		float rpm = Vehicle.Engine.RPM;
		float load = MathF.Abs( Vehicle.DbgThrottle );

		for ( int i = 0; i < Layers.Count && i < _runtimes.Count; i++ )
			_runtimes[i].Update( Layers[i], WorldPosition, rpm, load );
	}

	/// <summary>
	/// Determines the surface type from a Surface resource name.
	/// Returns 0 = asphalt, 1 = grass/dirt, 2 = sand/gravel.
	/// </summary>
	private int GetSurfaceType( Surface surface )
	{
		if ( surface == null ) return 0;

		string name = surface.ResourceName?.ToLowerInvariant() ?? "";

		if ( name.Contains( "grass" ) || name.Contains( "dirt" ) || name.Contains( "mud" ) || name.Contains( "earth" ) )
			return 1;
		if ( name.Contains( "sand" ) || name.Contains( "gravel" ) || name.Contains( "rock" ) || name.Contains( "snow" ) )
			return 2;

		return 0; // asphalt / default
	}

	private void UpdateTireSounds()
	{
		float skid = 0f;
		float asphaltCount = 0f;
		float grassCount = 0f;
		float sandCount = 0f;
		int groundedCount = 0;

		foreach ( var wheel in Vehicle.AllWheels() )
		{
			if ( !wheel.IsValid() || !wheel.IsGrounded )
				continue;

			groundedCount++;

			// Skid intensity per wheel
			float slipX = MathF.Abs( wheel.SlipRatio );
			skid += slipX.MapRange( 0.9f, 1.2f, 0f, 1f ).Clamp( 0f, 1f )
				* wheel.Fz.MapRange( 500f, 2000f, 0f, 1f ).Clamp( 0f, 1f );

			float slipY = MathF.Abs( wheel.DynamicSlipAngle ) / 90f;
			skid += slipY.MapRange( 0.4f, 1f, 0f, 0.5f ).Clamp( 0f, 0.5f )
				* wheel.Fz.MapRange( 1000f, 3000f, 0f, 0.5f ).Clamp( 0f, 0.5f );

			// Surface type voting
			int surfType = GetSurfaceType( wheel.HitSurface );
			if ( surfType == 1 ) grassCount++;
			else if ( surfType == 2 ) sandCount++;
			else asphaltCount++;
		}

		_smoothSkid = MathX.Lerp( _smoothSkid, skid, Time.Delta * 20f );

		// Compute surface weights (smooth transition)
		float total = groundedCount > 0 ? groundedCount : 1f;
		float targetAsphalt = asphaltCount / total;
		float targetGrass = grassCount / total;
		float targetSand = sandCount / total;

		float blendSpeed = Time.Delta * 5f;
		_asphaltWeight = MathX.Lerp( _asphaltWeight, targetAsphalt, blendSpeed );
		_grassWeight = MathX.Lerp( _grassWeight, targetGrass, blendSpeed );
		_sandWeight = MathX.Lerp( _sandWeight, targetSand, blendSpeed );

		// Volume: skid intensity + rolling sound based on speed
		float speedKmh = MathF.Abs( Vehicle.DbgCarSpeed * 3.6f );
		float rollingVol = speedKmh.MapRange( 5f, RollingFullSpeedKmh, 0f, RollingMaxVolume ).Clamp( 0f, RollingMaxVolume );
		float skidVol = _smoothSkid.MapRange( 0f, 2f, 0f, 1f ).Clamp( 0f, 1f ) * ScreechMaxVolume;
		float baseVolume = MathF.Max( rollingVol, skidVol );

		// Pitch: increases slightly with speed
		float basePitch = speedKmh.MapRange( 0f, 120f, 0.8f, 1.1f ).Clamp( 0.8f, 1.1f );

		// Update each surface sound channel
		UpdateSurfaceHandle( ref _asphaltHandle, TireAsphaltLoop, baseVolume * _asphaltWeight, basePitch );
		UpdateSurfaceHandle( ref _grassHandle, TireGrassLoop, baseVolume * _grassWeight, basePitch );
		UpdateSurfaceHandle( ref _sandHandle, TireSandLoop, baseVolume * _sandWeight, basePitch );
	}

	private void UpdateSurfaceHandle( ref SoundHandle handle, SoundEvent clip, float volume, float pitch )
	{
		if ( clip == null )
		{
			handle?.Stop();
			handle = null;
			return;
		}

		if ( volume > 0.01f )
		{
			if ( handle == null || handle.IsStopped )
			{
				handle = Sound.Play( clip, WorldPosition );
				if ( _mixer != null )
					handle.TargetMixer = _mixer;
			}

			handle.Volume = volume;
			handle.Pitch = pitch;
			handle.Position = WorldPosition;
		}
		else if ( handle != null )
		{
			handle.Stop();
			handle = null;
		}
	}

	private void UpdateFuelWarning()
	{
		if ( !_engineRunning || !Vehicle.IsValid() || !Vehicle.Engine.IsValid() )
			return;

		if ( !Vehicle.Engine.IsLowFuel )
		{
			_fuelWarningTimer = 0f;
			return;
		}

		if ( FuelWarningBeep == null )
			return;

		_fuelWarningTimer -= Time.Delta;
		if ( _fuelWarningTimer <= 0f )
		{
			_fuelWarningTimer = FuelWarningInterval;
			Sound.Play( FuelWarningBeep, WorldPosition );
		}
	}

	protected override void OnDestroy()
	{
		foreach ( var rt in _runtimes )
			rt.Stop();
		_runtimes.Clear();

		_asphaltHandle?.Stop();
		_asphaltHandle = null;
		_grassHandle?.Stop();
		_grassHandle = null;
		_sandHandle?.Stop();
		_sandHandle = null;
	}
}
