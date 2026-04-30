namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Predefined tire configuration based on Pacejka "Magic Formula" parameters.
/// Create presets for different tire types and assign them to wheels.
/// </summary>
[AssetType( Name = "Tire Preset", Extension = "tire" )]
public class TirePreset : GameResource
{
	[Property, Group( "Info" )] public string DisplayName { get; set; } = "Custom Tire";

	/// <summary>Friction coefficient (surface grip multiplier).</summary>
	[Property, Group( "Grip" )] public float FrictionMu { get; set; } = 0.9f;

	/// <summary>Load sensitivity exponent for power-based grip model.</summary>
	[Property, Group( "Grip" )] public float LoadSensitivity { get; set; } = 0.8f;

	/// <summary>Nominal load used to compute GripScale.</summary>
	[Property, Group( "Grip" )] public float NominalLoad { get; set; } = 1400f;

	/// <summary>Rolling resistance coefficient.</summary>
	[Property, Group( "Grip" )] public float RollingResistance { get; set; } = 0.02f;

	// ── Lateral Pacejka ──────────────────────────────────────────────────────

	/// <summary>Lateral stiffness (B). Higher = grip peaks at smaller slip angles.</summary>
	[Property, Group( "Lateral Pacejka" )] public float LatB { get; set; } = 10.0f;

	/// <summary>Lateral shape (C). Higher = sharper falloff after peak.</summary>
	[Property, Group( "Lateral Pacejka" )] public float LatC { get; set; } = 1.3f;

	/// <summary>Lateral peak (D). Maximum lateral grip factor (0-1).</summary>
	[Property, Group( "Lateral Pacejka" )] public float LatD { get; set; } = 1.0f;

	/// <summary>Lateral curvature (E). Controls the shape near the peak.</summary>
	[Property, Group( "Lateral Pacejka" )] public float LatE { get; set; } = 0.97f;

	// ── Longitudinal Pacejka ─────────────────────────────────────────────────

	/// <summary>Longitudinal stiffness (B). Higher = grip peaks at smaller slip ratios.</summary>
	[Property, Group( "Longitudinal Pacejka" )] public float LongB { get; set; } = 12.0f;

	/// <summary>Longitudinal shape (C).</summary>
	[Property, Group( "Longitudinal Pacejka" )] public float LongC { get; set; } = 2.0f;

	/// <summary>Longitudinal peak (D). Maximum longitudinal grip factor (0-1).</summary>
	[Property, Group( "Longitudinal Pacejka" )] public float LongD { get; set; } = 1.0f;

	/// <summary>Longitudinal curvature (E).</summary>
	[Property, Group( "Longitudinal Pacejka" )] public float LongE { get; set; } = 0.97f;
}
