using Sandbox.Events;
using OpenFramework.Utility;

namespace OpenFramework.World;

[Icon( "grass" )]
[Title( "Weed Jar" )]
[Category( "Roleplay" )]
[EditorHandle( "editor/component_icons/weed_pot.svg" )]
public class WeedJar : Component, IDescription, IGameEventHandler<KillEvent>, IUse
{
	// --- Section Informations ---
	[Property, Group( "Info" )]
	public string DisplayName { get; set; } = "Weed Jar";
	public GrabAction GetGrabAction() => GrabAction.SweepLeft;

	public UseResult CanUse( PlayerPawn player )
	{
		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		
	}

	public void OnGameEvent( KillEvent eventArgs )
	{
		if ( eventArgs.DamageInfo.Victim.GameObject == GameObject )
		{
			GameObject.Destroy();
		}
	}
}
