using Sandbox;

namespace OpenFramework;

[Title( "Vehicle Information" )]
[Category( "Vehicle" )]
public sealed class VehicleInformation : Component
{
	[Property] public string VehicleName { get; set; } = "Vehicle";

	[Property, Group( "Physics" ), Range( 300f, 8000f )]
	public float Mass { get; set; } = 1200f;

	[Property, Group( "Engine" ), Range( 100f, 20000f )]
	[Description( "Force motrice par roue motrice (N)." )]
	public float EnginePower { get; set; } = 1500f;

	[Property, Group( "Engine" ), Range( 100f, 5000f )]
	[Description( "Vitesse max (unités/s)." )]
	public float MaxSpeed { get; set; } = 800f;

	[Property, Group( "Engine" ), Range( 0f, 10f )]
	[Description( "Résistance au relâchement accélérateur." )]
	public float EngineBraking { get; set; } = 0f;

	[Property, Group( "Braking" ), Range( 100f, 10000f )]
	public float BrakeForce { get; set; } = 3000f;

	[Property, Group( "Braking" ), Range( 100f, 15000f )]
	public float HandbrakeForce { get; set; } = 5000f;

	[Property, Group( "Steering" ), Range( 5f, 55f )]
	public float MaxSteerAngle { get; set; } = 35f;

	[Property, Group( "Steering" ), Range( 1f, 15f )]
	public float SteerSpeed { get; set; } = 5f;

	[Property, Group( "Steering" ), Range( 0f, 1f )]
	[Description( "Réduction braquage à haute vitesse." )]
	public float SteerLimitAtSpeed { get; set; } = 0.4f;

	[Property, Group( "Suspension" ), Range( 5f, 200f )]
	[Description( "Longueur du rayon. Doit être > distance entre le GO roue et le sol." )]
	public float SuspensionLength { get; set; } = 20;

	[Property, Group( "Suspension" ), Range( 100f, 100000f )]
	[Description( "Raideur. Augmentez si le véhicule s'affaisse, baissez s'il lévite." )]
	public float SpringStrength { get; set; } = 8000f;

	[Property, Group( "Suspension" ), Range( 10f, 5000f )]
	[Description( "Amortisseur. Environ SpringStrength / 15." )]
	public float SpringDamper { get; set; } = 0;

	[Property, Group( "Grip" ), Range( 0f, 5000f )]
	[Description( "Force anti-dérapage latéral (N). Commencez à 800." )]
	public float LateralGripForce { get; set; } = 0;

	[Property, Group( "Grip" ), Range( 0f, 1f )]
	public float HandbrakeGrip { get; set; } = 0.05f;

	[Property, Group( "Wheels" ), Range( 1f, 60f )]
	[Description( "Rayon visuel de la roue pour l'animation." )]
	public float WheelRadius { get; set; } = 20f;
}
