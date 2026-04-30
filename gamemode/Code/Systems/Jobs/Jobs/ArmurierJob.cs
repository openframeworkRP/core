using Facepunch;
using OpenFramework.UI.QuickMenuSystem;

namespace OpenFramework.Systems.Jobs;

public sealed class ArmurierJob : JobComponent
{
	public override string JobIdentifier => "armurier";

	public override List<MenuItem> InteractionActions(PlayerPawn player)
	{
		base.InteractionActions(player);

		var list = new List<MenuItem>();

		var _self = Client.Local;                                                                                                                                                                                                                                                                                                                                                                                                                     
		/*
		list.Add( new MenuItem( "Mettre une amende", () =>
		{
			if ( !PlayerToPlayerActionMenu.RequireProximity( player, Constants.Instance.InteractionDistance ) )
			{
				_self.Notify( NotificationType.Error, $"Vous êtes trop loin de {player.DisplayName}" );
				QuickMenu.Close();
				return;
			}

			Fine( player );
		} ) );
		*/

		return list;
	}
}
