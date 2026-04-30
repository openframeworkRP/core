using Facepunch;
using Sandbox.Events;
using OpenFramework.Utility;

namespace OpenFramework.World;

public class WaterContainer : Component, IDescription, IGameEventHandler<KillEvent>
{
	[Property]
	public string DisplayName { get; set; } = "Water Container";

	public void OnGameEvent( KillEvent eventArgs )
	{
		if(eventArgs.DamageInfo.Victim.GameObject == GameObject)
		{
			GameObject.Destroy();
		}
	}
}
