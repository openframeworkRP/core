using Facepunch;
using OpenFramework.World.Devices;
using static Facepunch.NotificationSystem;

namespace OpenFramework.World;

[Title( "Police Computer" ), Icon( "computer" ), Group( "World" )]
public sealed class PoliceComputer : BaseDevice
{
	[Property] public HighlightOutline Outline { get; set; }

	protected override void OnStart()
	{
		base.OnStart();

		if ( Outline.IsValid() )
			Outline.Enabled = false;

		if ( ScreenUI.IsValid() )
			ScreenUI.Enabled = false;
	}

	/// <summary>
	/// Appelé côté client quand le joueur appuie long E sur le terminal.
	/// Vérifie le job localement avant d'activer l'écran.
	/// </summary>
	public void Open()
	{
		var job = Client.Local?.Data?.Job?.ToLower();
		if ( job != "police" )
		{
			Client.Local?.Notify( NotificationType.Error, "Accès réservé à la police." );
			return;
		}

		PowerOn();
	}

	/// <summary>Active ou désactive le surlignage de survol (appelé depuis PlayerPawn).</summary>
	public void SetHover( bool active )
	{
		if ( !Outline.IsValid() ) return;
		Outline.Enabled = active;
	}
}
