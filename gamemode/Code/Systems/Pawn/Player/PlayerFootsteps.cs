using Sandbox.Audio;

namespace OpenFramework.Systems.Pawn;

/// <summary>
/// Produces footstep sounds for the player.
/// </summary>
public sealed class PlayerFootsteps : Component
{
	[Property] public PlayerPawn Player { get; set; }
	[Property] public float FootstepBaseDecibels { get; set; }
	[Property] public float FootstepScale { get; set; }
	[Property] public float SprintFootstepScale { get; set; }

	public TimeSince TimeSinceStep { get; private set; }

	private bool flipFlop = false;

	/// <summary>
	/// Returns how often the player should make steps based on movement state.
	/// </summary>
	public float GetStepFrequency()
	{
		if ( Player.IsSprinting ) return 0.25f;
		if ( Player.IsCrouching || Player.IsSlowWalking ) return 0.6f;

		return 0.35f;
	}

	/// <summary>
	/// Plays a footstep sound depending on surface and state.
	/// </summary>
	private void Footstep()
	{
		var tr = Scene.Trace
			.Ray( Player.WorldPosition + Vector3.Up * 20, Player.WorldPosition + Vector3.Up * -20 )
			.IgnoreGameObjectHierarchy( Player.GameObject )
			.Run();

		if ( !tr.Hit || tr.Surface is null ) return;

		TimeSinceStep = 0;
		flipFlop = !flipFlop;

		if ( Player.IsCrouching || Player.IsSlowWalking ) return;

		var sound = flipFlop ? tr.Surface.SoundCollection.FootLeft : tr.Surface.SoundCollection.FootRight;
		if ( sound is null ) return;

		sound.Occlusion = true;
		sound.Distance = 1500f; // distance max
		sound.Volume = FootstepScale; // applique ton scale

		var handle = Sound.Play( sound, Player.WorldPosition );
		if ( !handle.IsValid() ) return;

		handle.DistanceAttenuation = true;
		handle.SpacialBlend = 1f; // toujours spatial pour tous
	}




	protected override void OnFixedUpdate()
	{
		if ( !Player.IsValid() )
			return;

		if ( Player.HealthComponent.State != LifeState.Alive )
			return;

		if ( TimeSinceStep < GetStepFrequency() )
			return;

		if ( Player.CharacterController.Velocity.Length > 50f )
		{
			Footstep();
		}
	}
}
