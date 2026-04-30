using Facepunch;
using OpenFramework.Extension;
using OpenFramework.UI.QuickMenuSystem;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;

public sealed class MaintenanceJob : JobComponent
{
	public override string JobIdentifier => "maintenance";

	public override void OnJoin( Client client )
	{
		base.OnJoin( client );
		client.Notify( NotificationType.Success, "Bienvenue dans l'équipe d'entretien de la ville !" );
	}
}
