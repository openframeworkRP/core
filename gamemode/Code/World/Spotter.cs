namespace Facepunch;

/// <summary>
/// Controlls the spotting behaviour of a player.
/// </summary>
public sealed class Spotter : Component
{
	public static float Interval = 0.5f;
	private static float SpottableListInterval = 2f;

	[Property] PlayerPawn Player { get; set; }

	private TimeSince LastPoll;
	private TimeSince LastSpottableRefresh;
	private Spottable[] _cachedSpottables = Array.Empty<Spottable>();

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( !Player.IsValid() )
			return;

		if ( !Player.HealthComponent.IsValid() )
			return;

		if ( Player.HealthComponent.State != LifeState.Alive )
			return;

		if ( LastSpottableRefresh > SpottableListInterval )
		{
			_cachedSpottables = Scene.GetAllComponents<Spottable>().ToArray();
			LastSpottableRefresh = 0;
		}

		if ( LastPoll < Interval )
			return;

		foreach ( var spottable in _cachedSpottables )
		{
			if ( !spottable.IsValid() || spottable.GameObject == this.GameObject )
				continue;

			Poll( spottable );
		}

		LastPoll = 0;
	}

	private void Poll( Spottable spottable )
	{
		var playerEyePos = Player.AimRay.Position;

		const float FOV = 85;
		float angle = Vector3.GetAngle( Player.EyeAngles.Forward, spottable.WorldPosition - playerEyePos );
		if ( MathF.Abs( angle ) > (FOV / 2) )
		{
			return;
		}

		// Try the top
		var trace = Scene.Trace.Ray( playerEyePos, spottable.WorldPosition + Vector3.Up * spottable.Height ) // bit of error for funsies
				.IgnoreGameObjectHierarchy( spottable.GameObject )
				.IgnoreGameObjectHierarchy( Player.GameObject )
				.UseHitboxes()
				.WithoutTags( "trigger", "ragdoll", "movement", "player" )
				.Run();

		if ( trace.Hit )
		{
			// Try the bottom
			trace = Scene.Trace.Ray( playerEyePos, spottable.WorldPosition )
				.IgnoreGameObjectHierarchy( spottable.GameObject )
				.IgnoreGameObjectHierarchy( Player.GameObject )
				.UseHitboxes()
				.WithoutTags( "trigger", "ragdoll", "movement", "player" )
				.Run();

			if ( trace.Hit )
				return;
		}

		spottable.Spotted(this);
	}
}
