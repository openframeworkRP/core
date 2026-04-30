using Sandbox;

namespace OpenFramework;

/// <summary>
/// Joue le son de greeting pré-généré d'un NPC à sa position.
/// </summary>
public sealed class NpcTtsService : Component
{
	public static NpcTtsService Instance { get; private set; }

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this )
			Instance = null;
	}

	/// <summary>
	/// Joue le SoundEvent de greeting du NPC à sa position.
	/// </summary>
	public void SpeakAt( NpcLogical npc )
	{
		if ( npc?.GreetingSound == null ) return;

		var handle = Sound.Play( npc.GreetingSound, npc.WorldPosition );
		if ( handle.IsValid() )
		{
			handle.Volume = 1.5f;
		}
	}
}
