using OpenFramework.UI.RadialMenu;
using OpenFramework.World.Devices;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial sur soi-même — actions rapides du joueur.
/// </summary>
public class SelfRadialMenu : RadialMenuBase
{
	public override string HoldKey => "menu"; // Touche différente pour le self menu

	public override List<RadialMenuItem> BuildItems() => new()
	{
		new()
		{
			Label      = "Téléphone",
			Icon       = "ui/icons/apps/phone_icon.svg",
			Color      = "#34C759",
			OnSelected = () => TogglePhone(),
		},
		new()
		{
			Label      = "Mains en l'air",
			Icon       = "ui/icons/hands_up.svg",
			Color      = "#fdea60",
			OnSelected = () => HandsUp(),
		},
		new()
		{
			Label      = "S'asseoir",
			Icon       = "ui/icons/chair.svg",
			Color      = "#007AFF",
			OnSelected = () => Sit(),
		},
		new()
		{
			Label      = "Fermer",
			Icon       = "ui/icons/close.svg",
			Color      = "#ffffff",
			OnSelected = () => { },
		},
	};

	private void TogglePhone()
	{

	}

	private void HandsUp() => Log.Info( $"[SelfRadialMenu] {Caller?.DisplayName} lève les mains" );
	private void Sit()     => Log.Info( $"[SelfRadialMenu] {Caller?.DisplayName} s'assoit" );
}
