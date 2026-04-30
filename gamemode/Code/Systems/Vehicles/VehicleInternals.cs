using OpenFramework.Utility;

namespace OpenFramework.Systems.Vehicles;

// ── Persistence DTOs ──────────────────────────────────────────────────────────

public class TireData
{
	public float WearPct { get; set; } = 100f;
	public float Km { get; set; }
	public bool IsFlat { get; set; }
}

public class VehicleInternalsData
{
	public float EngineHealthPct { get; set; } = 100f;
	public float EngineKm { get; set; }

	public float GearboxHealthPct { get; set; } = 100f;
	public float GearboxKm { get; set; }

	public float TurboHealthPct { get; set; } = 100f;
	public float TurboKm { get; set; }

	public List<TireData> Tires { get; set; } = new();
}

// ── Runtime tire state ────────────────────────────────────────────────────────

public class TireState
{
	public Wheel Wheel { get; set; }
	public float WearPct { get; set; } = 100f;
	public float Km { get; set; }
	public bool IsFlat { get; set; }
}

// ── Component ─────────────────────────────────────────────────────────────────

[Category( "Vehicles" )]
[Title( "Vehicle Internals" )]
[Icon( "build" )]
public sealed class VehicleInternals : Component
{
	// ── References ─────────────────────────────────────────────────────────

	[Property] public Vehicle Vehicle { get; set; }

	// ── Lifespan settings (km) ────────────────────────────────────────────

	[Property, Group( "Lifespan" )] public float EngineLifespanKm { get; set; } = 3000f;
	[Property, Group( "Lifespan" )] public float GearboxLifespanKm { get; set; } = 5000f;
	[Property, Group( "Lifespan" )] public float TireLifespanKm { get; set; } = 1500f;
	[Property, Group( "Lifespan" )] public float TurboLifespanKm { get; set; } = 2000f;

	// ── Red-zone thresholds ───────────────────────────────────────────────

	[Property, Group( "Red Zone" )] public float RedZoneThreshold { get; set; } = 15f;
	[Property, Group( "Red Zone" )] public float RedZoneMinFactor { get; set; } = 0.15f;

	// ── Synced state ──────────────────────────────────────────────────────

	[Sync( SyncFlags.FromHost )] public float EngineHealthPct { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float EngineKm { get; set; }

	[Sync( SyncFlags.FromHost )] public float GearboxHealthPct { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float GearboxKm { get; set; }

	[Sync( SyncFlags.FromHost )] public float TurboHealthPct { get; set; } = 100f;
	[Sync( SyncFlags.FromHost )] public float TurboKm { get; set; }

	// ── Per-wheel tire state ──────────────────────────────────────────────

	public List<TireState> Tires { get; private set; } = new();

	// ── Derived helpers ───────────────────────────────────────────────────

	public float WorstTireWearPct => Tires.Count == 0 ? 100f : Tires.Min( t => t.WearPct );
	public bool HasFlatTire => Tires.Any( t => t.IsFlat );
	public bool IsEngineHS => EngineHealthPct <= 0f;
	public bool IsGearboxHS => GearboxHealthPct <= 0f;
	public bool IsImmobilized => IsEngineHS || IsGearboxHS || Tires.All( t => t.IsFlat );

	/// <summary>Engine power multiplier based on health (1.0 = full, degrades in red zone).</summary>
	public float EnginePowerFactor => CalcRedZoneFactor( EngineHealthPct );

	/// <summary>Gearbox shift-speed multiplier (1.0 = normal, slower when degraded).</summary>
	public float GearboxShiftFactor => CalcRedZoneFactor( GearboxHealthPct );

	// ── Persistence ───────────────────────────────────────────────────────

	private float _saveTimer;
	private const float SaveInterval = 5f;

	// ── Lifecycle ─────────────────────────────────────────────────────────

	protected override void OnStart()
	{
		if ( !Vehicle.IsValid() ) return;

		InitTires();
		Load();
	}

	private void InitTires()
	{
		Tires.Clear();
		foreach ( var w in Vehicle.AllWheels() )
		{
			Tires.Add( new TireState { Wheel = w } );
		}
	}

	private void Load()
	{
		if ( Vehicle.VehicleId == Guid.Empty ) return;

		var data = FileSystem.Data.ReadJson<VehicleInternalsData>( $"vehicles/{Vehicle.VehicleId}_internals.json" );
		if ( data == null ) return;

		EngineHealthPct = data.EngineHealthPct;
		EngineKm = data.EngineKm;
		GearboxHealthPct = data.GearboxHealthPct;
		GearboxKm = data.GearboxKm;
		TurboHealthPct = data.TurboHealthPct;
		TurboKm = data.TurboKm;

		for ( int i = 0; i < Math.Min( data.Tires.Count, Tires.Count ); i++ )
		{
			Tires[i].WearPct = data.Tires[i].WearPct;
			Tires[i].Km = data.Tires[i].Km;
			Tires[i].IsFlat = data.Tires[i].IsFlat;
		}
	}

	private void Save()
	{
		if ( Vehicle.VehicleId == Guid.Empty ) return;

		var data = new VehicleInternalsData
		{
			EngineHealthPct = EngineHealthPct,
			EngineKm = EngineKm,
			GearboxHealthPct = GearboxHealthPct,
			GearboxKm = GearboxKm,
			TurboHealthPct = TurboHealthPct,
			TurboKm = TurboKm,
			Tires = Tires.Select( t => new TireData
			{
				WearPct = t.WearPct,
				Km = t.Km,
				IsFlat = t.IsFlat
			} ).ToList()
		};

		FileSystem.Data.WriteJson( $"vehicles/{Vehicle.VehicleId}_internals.json", data );
	}

	// ── Public API ────────────────────────────────────────────────────────

	/// <summary>Called by Vehicle each physics tick with distance driven this frame (km).</summary>
	public void AddDistance( float deltaKm )
	{
		if ( deltaKm <= 0f ) return;

		// Engine
		if ( EngineHealthPct > 0f )
		{
			EngineKm += deltaKm;
			EngineHealthPct = Math.Clamp( 100f * (1f - EngineKm / EngineLifespanKm), 0f, 100f );
		}

		// Gearbox
		if ( GearboxHealthPct > 0f )
		{
			GearboxKm += deltaKm;
			GearboxHealthPct = Math.Clamp( 100f * (1f - GearboxKm / GearboxLifespanKm), 0f, 100f );
		}

		// Turbo
		if ( TurboHealthPct > 0f && Vehicle.ForcedInduction.IsValid() )
		{
			TurboKm += deltaKm;
			TurboHealthPct = Math.Clamp( 100f * (1f - TurboKm / TurboLifespanKm), 0f, 100f );
		}

		// Tires
		foreach ( var tire in Tires )
		{
			if ( tire.IsFlat || tire.WearPct <= 0f ) continue;
			tire.Km += deltaKm;
			tire.WearPct = Math.Clamp( 100f * (1f - tire.Km / TireLifespanKm), 0f, 100f );
		}

		// Auto-save periodically
		_saveTimer += deltaKm;
		if ( _saveTimer >= SaveInterval )
		{
			_saveTimer = 0f;
			Save();
		}
	}

	/// <summary>Flatten a specific tire by wheel reference.</summary>
	public void FlattenTire( Wheel wheel )
	{
		var tire = Tires.FirstOrDefault( t => t.Wheel == wheel );
		if ( tire != null ) tire.IsFlat = true;
	}

	/// <summary>Flatten tire by index.</summary>
	public void FlattenTire( int index )
	{
		if ( index >= 0 && index < Tires.Count )
			Tires[index].IsFlat = true;
	}

	/// <summary>Replace a tire (reset wear + remove flat).</summary>
	public void ReplaceTire( int index )
	{
		if ( index < 0 || index >= Tires.Count ) return;
		Tires[index].WearPct = 100f;
		Tires[index].Km = 0f;
		Tires[index].IsFlat = false;
	}

	/// <summary>Replace all tires.</summary>
	public void ReplaceAllTires()
	{
		for ( int i = 0; i < Tires.Count; i++ )
			ReplaceTire( i );
	}

	/// <summary>Get tire state for a specific wheel.</summary>
	public TireState GetTireForWheel( Wheel wheel )
	{
		return Tires.FirstOrDefault( t => t.Wheel == wheel );
	}

	/// <summary>Grip factor for a specific wheel (0-1). Flat = 0.1, otherwise scales with wear.</summary>
	public float GetTireGripFactor( Wheel wheel )
	{
		var tire = GetTireForWheel( wheel );
		if ( tire == null ) return 1f;
		if ( tire.IsFlat ) return 0.1f;
		return CalcRedZoneFactor( tire.WearPct );
	}

	// ── Internal ──────────────────────────────────────────────────────────

	private float CalcRedZoneFactor( float healthPct )
	{
		if ( healthPct <= 0f ) return 0f;
		if ( healthPct >= RedZoneThreshold ) return 1f;
		float t = healthPct / RedZoneThreshold;
		return MathX.Lerp( RedZoneMinFactor, 1f, t );
	}
}
