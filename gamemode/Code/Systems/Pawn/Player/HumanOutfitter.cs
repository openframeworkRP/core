using Sandbox.Events;

namespace OpenFramework.Systems.Pawn;

public sealed class HumanOutfitter : Component
{
	[Property] public PlayerPawn PlayerPawn { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[KeyProperty] public List<Model> Models { get; set; }

	/// <summary>
	/// Called to wear an outfit based off a team.
	/// </summary>
	/// <param name="team"></param>
	[Rpc.Broadcast( NetFlags.HostOnly )]
	public void UpdateFromTeam()
	{
		Renderer.Model = Game.Random.FromList( Models );
		PlayerPawn.Body.Refresh();
	}
}
