using System.Threading.Tasks;
using OpenFramework.Systems.Pawn;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	/// <summary>
	/// Commande admin : teste les degats de chute a differentes hauteurs.
	/// Teleporte le joueur au-dessus de sa position actuelle, attend qu'il atterrisse,
	/// et releve les degats subis a chaque palier.
	/// </summary>
	[Command( "Test Fall Damage", ["testfalldmg", "testfall"], "Teste les degats de chute a plusieurs hauteurs", "ui/icons/admin.svg", CommandPermission.Admin )]
	public static void TestFallDamage()
	{
		_ = RunFallDamageTest();
	}

	private static async Task RunFallDamageTest()
	{
		var client = Client.Local;
		if ( client == null || !client.IsValid() )
		{
			Log.Error( "[TestFallDmg] Client local invalide" );
			return;
		}

		if ( !client.IsAdmin )
		{
			client.Notify( NotificationType.Error, "Acces refuse !" );
			return;
		}

		var pawn = client.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			client.Notify( NotificationType.Error, "Pas de pawn actif." );
			return;
		}

		var startPos = pawn.WorldPosition;
		var heights = new[] { 200f, 320f, 400f, 500f, 600f, 800f, 1000f, 1200f, 1500f };

		Log.Info( "[TestFallDmg] ========================================" );
		Log.Info( $"[TestFallDmg] Debut — position de depart: {startPos}" );
		Log.Info( "[TestFallDmg] hauteur(u) | metres | HP avant | HP apres | degats | vel impact" );
		Log.Info( "[TestFallDmg] ----------------------------------------" );

		client.Notify( NotificationType.Info, "Test chute: debut. Reste immobile, ne bouge pas." );

		foreach ( var h in heights )
		{
			RPC_HealPlayer( client, -1 );
			await GameTask.DelaySeconds( 0.5f );

			if ( !pawn.IsValid() || pawn.HealthComponent == null )
				break;

			var hpBefore = pawn.HealthComponent.Health;
			var targetPos = startPos + Vector3.Up * h;

			pawn.ForceTeleport( targetPos );

			await GameTask.DelaySeconds( 0.3f );

			float peakVel = 0f;
			float elapsed = 0f;
			const float timeout = 15f;

			while ( elapsed < timeout )
			{
				await GameTask.DelaySeconds( 0.05f );
				elapsed += 0.05f;

				if ( !pawn.IsValid() ) break;

				var cc = pawn.CharacterController;
				if ( cc.IsValid() )
				{
					var vz = MathF.Abs( cc.Velocity.z );
					if ( vz > peakVel ) peakVel = vz;

					if ( cc.IsOnGround && elapsed > 0.5f )
						break;
				}
			}

			await GameTask.DelaySeconds( 0.5f );

			var hpAfter = pawn.HealthComponent != null ? pawn.HealthComponent.Health : 0f;
			var dmg = hpBefore - hpAfter;
			var meters = h / 40f;

			Log.Info( $"[TestFallDmg] {h,6:0}u | {meters,5:0.0}m | {hpBefore,6:0.0} | {hpAfter,6:0.0} | {dmg,5:0.0} | {peakVel,6:0.0}" );

			if ( pawn.HealthComponent == null || pawn.HealthComponent.State == LifeState.Dead )
			{
				Log.Warning( $"[TestFallDmg] Mort a {h}u ({meters:0.0}m) — test arrete." );
				client.Notify( NotificationType.Warning, $"Test arrete: mort a {meters:0.0}m" );
				break;
			}

			await GameTask.DelaySeconds( 0.3f );
		}

		if ( pawn.IsValid() && pawn.HealthComponent != null && pawn.HealthComponent.State == LifeState.Alive )
		{
			RPC_HealPlayer( client, -1 );
			await GameTask.DelaySeconds( 0.2f );
			pawn.ForceTeleport( startPos );
		}

		Log.Info( "[TestFallDmg] ========================================" );
		Log.Info( "[TestFallDmg] Tests termines. Consulte la console pour le tableau." );
		client.Notify( NotificationType.Success, "Test chute: termine. Voir la console." );
	}
}
