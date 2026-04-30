using Facepunch;
using Facepunch.UI;
using OpenFramework.GameLoop;

namespace OpenFramework.Systems.Pawn;

public partial class PlayerPawn
{
	/// <summary>
	/// Development: should bots follow the player's input?
	/// </summary>
	[ConVar( "hc1_bot_follow" )] public static bool BotFollowHostInput { get; set; }

	[Rpc.Host]
	private static void Host_Suicide( PlayerPawn pawn )
	{
		if ( !pawn.IsValid() )
			return;

		pawn.HealthComponent.TakeDamage( new( pawn, float.MaxValue ) );
	}

	[ConCmd( "test_weight_speed" )]
	public static void TestWeightSpeed()
	{
		var player = Game.ActiveScene.GetAllComponents<PlayerPawn>().FirstOrDefault( x => !x.IsProxy );
		if ( player == null )
		{
			Log.Warning( "[TestWeightSpeed] Aucun joueur local trouvé." );
			return;
		}

		if ( player.SpeedTestActive )
		{
			Log.Warning( "[TestWeightSpeed] Test déjà en cours." );
			return;
		}

		var global = Constants.Instance;
		var inventory = player.InventoryContainer;
		float maxWeight = inventory?.MaxWeight ?? 50f;
		float realWeight = inventory?.CurrentWeight ?? 0f;

		Log.Info( "========================================" );
		Log.Info( "   TEST LIVE VITESSE / POIDS" );
		Log.Info( $"   MaxWeight: {maxWeight} kg | Poids actuel: {realWeight} kg" );
		Log.Info( $"   MaxWeightSpeedPenalty: {global.MaxWeightSpeedPenalty}" );
		Log.Info( "   Le joueur va avancer automatiquement." );
		Log.Info( "========================================" );
		Log.Info( "" );
		Log.Info( "--- Phase 0% du poids (0 kg) ---" );

		// Init du test
		player.SpeedTestActive = true;
		player.SpeedTestPhase = 0;
		player.SpeedTestWeightIndex = 0;
		player.SpeedTestTimer = 0f;
		player.SpeedTestMeasureTimer = 0f;
		player.SpeedTestTotalSpeed = 0f;
		player.SpeedTestSamples = 0;
		player.SpeedTestResults = new float[3][];
		player.SpeedTestResults[0] = new float[4];
		player.SpeedTestResults[1] = new float[4];
		player.SpeedTestResults[2] = new float[4];

		TestWeightRatioOverride = 0f;
	}

	[ConCmd( "test_weight_speed_stop" )]
	public static void StopWeightSpeedTest()
	{
		var player = Game.ActiveScene.GetAllComponents<PlayerPawn>().FirstOrDefault( x => !x.IsProxy );
		if ( player == null ) return;

		if ( !player.SpeedTestActive )
		{
			Log.Warning( "[TestWeightSpeed] Aucun test en cours." );
			return;
		}

		player.SpeedTestActive = false;
		TestWeightRatioOverride = null;
		player.IsCrouching = false;
		player.IsSlowWalking = false;
		player.IsSprinting = false;
		Log.Info( "[TestWeightSpeed] Test annulé." );
	}
}
