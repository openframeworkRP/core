namespace OpenFramework.Systems.Vehicles;

public enum DiffType
{
	/// <summary>Equal 50/50 split — simple but unrealistic.</summary>
	Open,

	/// <summary>Limited-Slip Differential — biases torque toward the wheel with more grip.</summary>
	LSD,

	/// <summary>Locked — both wheels forced to same speed. Maximum traction, least stable.</summary>
	Locked,

	/// <summary>Torsen — torque-sensing differential. Biases torque based on speed ratio up to TorsenBias limit.</summary>
	Torsen
}
