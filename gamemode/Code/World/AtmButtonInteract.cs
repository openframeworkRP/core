using OpenFramework.Systems.AtmSystem;
using OpenFramework.Systems.Pawn;

namespace OpenFramework.World;

public sealed class AtmButtonInteract : Component, IUse
{
	public AtmComponent Atm { get; private set; }

	protected override void OnStart()
	{
		Atm = Components.Get<AtmComponent>( FindMode.EverythingInSelfAndAncestors );
		if ( Atm == null )
			Log.Warning( $"[AtmButtonInteract] AtmComponent introuvable sur '{GameObject.Name}'" );
	}

	public UseResult CanUse( PlayerPawn player )
	{
		if ( Atm == null ) return "ATM non configuré.";
		return true;
	}

	public void OnUse( PlayerPawn player ) { }

	public GrabAction GetGrabAction() => GrabAction.PushButton;
}
