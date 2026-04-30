using Facepunch;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;

public sealed class CuisinierJob : JobComponent
{
	public override string JobIdentifier => "cuisinier";

	public override void OnJoin( Client client )
	{
		base.OnJoin( client );
		client.Notify( NotificationType.Success, "Bienvenue en cuisine ! Préparez de bons petits plats pour les habitants." );
	}
}
