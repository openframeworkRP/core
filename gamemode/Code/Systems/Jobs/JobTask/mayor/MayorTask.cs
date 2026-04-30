
using Facepunch;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;
public class MayorTask : Component, IUse
{
	[Property] public float Reward { get; set; } = 15f;

	public UseResult CanUse( PlayerPawn player )
	{
		return player.Client.Data.Job == "mayor";
	}

	public void OnUse( PlayerPawn player )
	{
		

	}

}
