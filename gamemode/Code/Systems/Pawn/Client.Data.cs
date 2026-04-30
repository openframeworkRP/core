using OpenFramework.Models;
using OpenFramework.Systems.Jobs;
using static Facepunch.NotificationSystem;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace OpenFramework.Systems.Pawn;

public class ClientData : Component
{
	[Property] public Client Client { get; set; }

	// Money & Economy
	//[Property, Sync( SyncFlags.FromHost ), MinMax( -100000, 1000000 )] public int Money { get; set; } = 500;
	[Property, Sync( SyncFlags.FromHost ), MinMax( -100000, 1000000 )] public int Bank { get; set; } = 500;

	//Props CurrentProps / MAX
	[Property, Sync( SyncFlags.FromHost )] public int CurrentProps { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public int MaxProps { get; set; } = 20;

	// Job
	[Property, Sync( SyncFlags.FromHost ), Sandbox.Change( "OnJobChange" )] public string Job { get; set; } = "citizen";
	[Property, Sync( SyncFlags.FromHost )] public Dictionary<TimeSpan, string> BankTransferHistory { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public string JobGrade { get; set; }

	// Admin Role
	[Property, Sync( SyncFlags.FromHost )] public string Role { get; set; } = "user";

	// Needs
	[Property, Sync( SyncFlags.FromHost )] public float Thirst { get; set; } = 100f;
	[Property, Sync( SyncFlags.FromHost )] public float Hunger { get; set; } = 100f;
	[Property, Sync( SyncFlags.FromHost )] public float Stamina { get; set; } = 100f;

	// Crime
	[Property, Sync( SyncFlags.FromHost )] public NetList<Fine> Fines { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public NetList<string> JailHistory { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public NetList<string> Warnings { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public int WantedLevel { get; set; } = 0;
	[Property, Sync( SyncFlags.FromHost )] public float CurrentJailTime { get; set; } = 0f;
	[Property] public bool IsInJail => Client.GetRemaining( Client.JailEndTime ) > 0;
	[Property, Sync( SyncFlags.FromHost )] public bool IsCuffed { get; set; } = false;

	// Identity
	[Property, Sync( SyncFlags.FromHost )] public string FirstName { get; set; } = "John";
	[Property, Sync( SyncFlags.FromHost )] public string LastName { get; set; } = "Doe";
	[Property, Sync( SyncFlags.FromHost )] public string BirthDate { get; set; } = "2000-01-01";

	// Position
	[Property, Sync( SyncFlags.FromHost )] public Vector3 LastPosition { get; set; } = Vector3.Zero;
	[Property, Sync( SyncFlags.FromHost )] public Rotation LastRotation { get; set; } = Rotation.Identity;

	// Licenses
	[Property, Sync( SyncFlags.FromHost )] public NetList<string> Licenses { get; set; } = new();

	// Properties & Vehicles
	[Property, Sync( SyncFlags.FromHost )] public NetList<string> OwnedVehicles { get; set; } = new();
	[Property, Sync( SyncFlags.FromHost )] public NetList<string> OwnedHouses { get; set; } = new();

	// Meta
	[Property, Sync( SyncFlags.FromHost )] public DateTime LastSeen { get; set; } = DateTime.UtcNow;
	[Property, Sync( SyncFlags.FromHost )] public int PlayTimeMinutes { get; set; } = 0;

	public void OnJobChange( string oldValue, string newValue )
	{
		if( Game.ActiveScene == null ) return;

		var oldJob = JobSystem.GetJob( oldValue );
		var newJob = JobSystem.GetJob( newValue );

		oldJob.OnLeave( Client );
		newJob.OnJoin( Client );
	}
}
