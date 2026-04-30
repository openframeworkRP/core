using Sandbox;

namespace OpenFramework;

/// <summary>
/// À placer sur chaque GameObject siège (Seat-UL, Seat-LL, etc.).
/// Gère le placement du joueur et sa caméra passager.
/// </summary>
[Title( "Seat Component" )]
[Category( "Vehicle" )]
public sealed class SeatComponent : Component
{
	[Property]
	[Description( "Le siège conducteur : active la DriverCamera du VehicleController." )]
	public bool IsDriverSeat { get; set; } = false;

	[Property]
	[Description( "Caméra dédiée à ce siège passager (laisser vide pour le siège conducteur)." )]
	public CameraComponent SeatCamera { get; set; }

	[Property]
	[Description( "Offset position du joueur par rapport à ce siège (ajustement fin)." )]
	public Vector3 PlayerOffset { get; set; } = Vector3.Zero;

	public bool IsOccupied => Occupant != null;
	public PlayerPawn Occupant { get; private set; }

	public void Occupy( PlayerPawn player )
	{
		Occupant = player;

		if ( !IsDriverSeat && SeatCamera.IsValid() )
			SeatCamera.Enabled = true;
	}

	public void Vacate()
	{
		if ( !IsDriverSeat && SeatCamera.IsValid() )
			SeatCamera.Enabled = false;

		Occupant = null;
	}

	protected override void OnFixedUpdate()
	{
		// Colle le joueur à la position du siège à chaque frame physique
		if ( Occupant != null )
		{
			Occupant.WorldPosition = WorldPosition + PlayerOffset;
			Occupant.WorldRotation = WorldRotation;
		}
	}
}
