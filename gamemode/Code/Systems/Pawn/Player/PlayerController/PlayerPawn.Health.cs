using Facepunch;
using Sandbox.Events;

namespace OpenFramework.Systems.Pawn;

public partial class PlayerPawn : IGameEventHandler<DamageTakenEvent>
{

	/// <summary>
	/// Called when YOU take damage from something
	/// </summary>
	void IGameEventHandler<DamageTakenEvent>.OnGameEvent( DamageTakenEvent eventArgs )
	{
		var damageInfo = eventArgs.DamageInfo;

		var attacker = GameUtils.GetPlayerFromComponent( eventArgs.DamageInfo.Attacker );
		var victim = GameUtils.GetPlayerFromComponent( eventArgs.DamageInfo.Victim );

		var position = eventArgs.DamageInfo.Position;
		var force = damageInfo.Force.IsNearZeroLength ? Random.Shared.VectorInSphere() : damageInfo.Force;

		if ( Body.IsValid() && Body.AnimationHelpers is not null )
		{
			foreach ( var helper in Body.AnimationHelpers )
			{
				if ( helper is null ) continue;
				helper.ProceduralHitReaction( damageInfo.Damage / 100f, force );
			}
		}

		if ( !damageInfo.Attacker.IsValid() )
			return;		

		TimeUntilAccelerationRecovered = Global.TakeDamageAccelerationDampenTime;
		AccelerationAddedScale = Global.TakeDamageAccelerationOffset;

		if ( attacker != victim && Body.IsValid() )
		{
			Body.DamageTakenPosition = position;
			Body.DamageTakenForce = force.Normal * damageInfo.Damage * Game.Random.Float( 5f, 20f );
		}

		// Headshot effects
		if ( damageInfo.Hitbox.HasFlag( HitboxTags.Head ) )
		{
			// Non-local viewer
			if ( !IsViewer )
			{
				var go = damageInfo.HasHelmet ? HeadshotWithHelmetEffect?.Clone( position ) : HeadshotEffect?.Clone( position );
			}

			var headshotSound = damageInfo.HasHelmet ? HeadshotWithHelmetSound : HeadshotSound;
			if ( headshotSound is not null )
			{
				var handle = Sound.Play( headshotSound, position );
				handle.SpacialBlend = (attacker.IsViewer || victim.IsViewer) ? 0 : handle.SpacialBlend;
			}
		}
		else
		{
			if ( BloodEffect.IsValid() )
			{
				BloodEffect.Clone( new CloneConfig()
				{
					StartEnabled = true,
					Transform = new( position + Vector3.Up * 50f),
					Parent = GameObject, // Lie l'effet au joueur
					Name = $"Blood effect from ({GameObject})"
				} );
			}
		}
	}
}
