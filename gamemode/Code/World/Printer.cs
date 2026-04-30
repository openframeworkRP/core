using Sandbox;
using OpenFramework.Command;
using System;

public class Printer : Component
{
	[Property, Sync( SyncFlags.FromHost )] public float Time { get; set; } = 1f;
	[Property, Sync( SyncFlags.FromHost )] public float MultiplierMoney { get; set; } = 0.25f;
	[Property, Sync( SyncFlags.FromHost )] public float Money { get; set; } = 0;
	[Property, Sync( SyncFlags.FromHost )] public float MaxMoney { get; set; } = 10;
	[Property, Sync( SyncFlags.FromHost )] public float Temperature { get; set; } = 60.0f;
	[Property, Sync( SyncFlags.FromHost )] public string Name { get; set; } = "Basic Printer";
	[Property, Sync( SyncFlags.FromHost )] public GameObject EjectMoney { get; set; }

	/// <summary>
	/// Sound Printer
	/// </summary>
	[Property, Sync( SyncFlags.FromHost )] public SoundPointComponent SoundPrinter { get; set; }

	/// <summary>
	///  Colling Options / Sound Options 
	/// </summary>
	[Property, FeatureEnabled( "Colling" )] public bool isColling { get; set; } = false;
	[Property, Sync( SyncFlags.FromHost ), Feature( "Colling" )] public List<GameObject> CollingList { get; set; }
	[Property, Sync( SyncFlags.FromHost ), Feature( "Colling" )] public SoundPointComponent SoundColling { get; set; }


	private TimeSince lastTick; // temps écoulé depuis le dernier tick d'argent
	private TimeSince lastHeat;

	private float heatAmount = 0.1f;   // °C ajouté à chaque intervalle
	private float heatInterval = 2f;

	protected override void OnStart()
	{
		// Initialisation (utile pour debugging)
		if ( !Networking.IsHost )
		{
			lastTick = 0f;
			lastHeat = 0f;
			Log.Info( $"[Printer] '{Name}' initialised on Client." );
		}
	}

	public void GetMoney()
	{
		//Commands.RPC_PrinterMoney(Money);
		Money = 0;
	}


	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return; // logique serveur uniquement

		// chauffe par paliers (lent)
		if ( lastHeat > heatInterval )
		{
			Temperature = MathF.Min( Temperature + heatAmount, 100f );
			lastHeat = 0f;
		}

		// génération d'argent — uniquement si pas plein
		if ( lastTick > Time && Money < MaxMoney )
		{
			Money += MultiplierMoney;

			// clamp pour éviter le dépassement par petits incréments
			if ( Money > MaxMoney ) Money = MaxMoney;

			lastTick = 0f;

			// log utile pour vérifier que le tick tourne bien sur le serveur
			Log.Info( $"[Printer] '{Name}' -> Money: {Money:0.##}/{MaxMoney}" );
		}
	}
}
