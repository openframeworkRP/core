namespace OpenFramework.Systems.Vehicles;

public partial class Vehicle
{
	public class VehicleInputState
	{
		public Vector3 direction;
		public bool isBoosting;
		public bool isHandbraking;
		public bool headlightsToggled;
		public bool turnSignalLeftPressed;
		public bool turnSignalRightPressed;
		public bool hazardLightsPressed;

		public void Reset()
		{
			direction = Vector3.Zero;
			isBoosting = false;
			isHandbraking = false;
			headlightsToggled = false;
			turnSignalLeftPressed = false;
			turnSignalRightPressed = false;
			hazardLightsPressed = false;
		}

		/// <summary>
		/// Legacy method — input is now handled by VehicleInputController
		/// with dedicated key bindings separate from on-foot actions.
		/// </summary>
		public void UpdateFromLocal()
		{
			// No longer used — VehicleInputController reads keys directly
		}
	}
}
