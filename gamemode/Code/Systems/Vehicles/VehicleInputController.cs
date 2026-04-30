using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Vehicles.UI;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Reads player input and feeds it into the Vehicle's InputState.
/// Movement uses Input.AnalogMove (respects keyboard layout AZERTY/QWERTY).
/// Special keys use Input.Keyboard for vehicle-specific bindings.
/// </summary>
public sealed class VehicleInputController : Component
{
	[Property, RequireComponent] public Vehicle Vehicle { get; set; }

	// ── Vehicle Key Bindings (for actions that don't exist in on-foot controls) ──
	[Property, Group( "Key Bindings" )] public string KeyHandbrake { get; set; } = "space";
	[Property, Group( "Key Bindings" )] public string KeyHeadlights { get; set; } = "f";
	[Property, Group( "Key Bindings" )] public string KeyTurnLeft { get; set; } = "left";
	[Property, Group( "Key Bindings" )] public string KeyTurnRight { get; set; } = "right";
	[Property, Group( "Key Bindings" )] public string KeyHazard { get; set; } = "down";

	protected override void OnUpdate()
	{
		if ( Vehicle == null )
			return;

		// Find the local player in any seat of this vehicle
		PlayerSeat localSeat = null;
		foreach ( var seat in Vehicle.Seats )
		{
			if ( seat.IsValid() && seat.Player.IsValid() && seat.Player.IsLocallyControlled )
			{
				localSeat = seat;
				break;
			}
		}

		if ( localSeat == null )
			return;

		// Driver: send input to the host so the server runs physics
		if ( localSeat.HasInput )
		{
			// AnalogMove respects keyboard layout (AZERTY/QWERTY)
			var dir = Input.AnalogMove;
			var boost = Input.Down( "Run" );
			var handbrake = Input.Keyboard.Down( KeyHandbrake );
			var headlights = Input.Keyboard.Pressed( KeyHeadlights );
			var turnLeft = Input.Keyboard.Pressed( KeyTurnLeft );
			var turnRight = Input.Keyboard.Pressed( KeyTurnRight );
			var hazard = Input.Keyboard.Pressed( KeyHazard );

			SendInputToHost( dir, boost, handbrake, headlights, turnLeft, turnRight, hazard );
		}

		// Any seat: open radial menu with exit option
		if ( Input.Pressed( "use" ) )
		{
			VehicleRadialMenu.OpenInVehicle( Vehicle, localSeat );
		}
	}

	[Rpc.Host]
	private void SendInputToHost( Vector3 direction, bool isBoosting, bool isHandbraking,
		bool headlightsToggled, bool turnSignalLeft, bool turnSignalRight, bool hazardLights )
	{
		if ( Vehicle == null ) return;

		Vehicle.InputState.direction = direction;
		Vehicle.InputState.isBoosting = isBoosting;
		Vehicle.InputState.isHandbraking = isHandbraking;
		Vehicle.InputState.headlightsToggled = headlightsToggled;
		Vehicle.InputState.turnSignalLeftPressed = turnSignalLeft;
		Vehicle.InputState.turnSignalRightPressed = turnSignalRight;
		Vehicle.InputState.hazardLightsPressed = hazardLights;
	}

	[Rpc.Host]
	private void RequestExit( PlayerPawn player )
	{
		if ( Vehicle == null ) return;

		var seat = Vehicle.Seats?.FirstOrDefault( s => s.Player == player );
		seat?.Leave( player );
	}
}
