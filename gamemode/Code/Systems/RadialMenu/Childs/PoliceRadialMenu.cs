using OpenFramework.UI.RadialMenu;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial de la police — interactions sur un joueur suspect.
/// </summary>
public class PoliceRadialMenu : RadialMenuBase
{
	public override List<RadialMenuItem> BuildItems() => new()
	{
		new()
		{
			Label      = "Menotter",
			Icon       = "ui/icons/handcuffs.svg",
			Color      = "#fdea60",
			OnSelected = () => Handcuff(),
		},
		new()
		{
			Label      = "Fouiller",
			Icon       = "ui/icons/search.svg",
			Color      = "#007AFF",
			OnSelected = () => Search(),
		},
		new()
		{
			Label      = "Arrêter",
			Icon       = "ui/icons/jail.svg",
			Color      = "#e25050",
			OnSelected = () => Arrest(),
		},
		new()
		{
			Label      = "Libérer",
			Icon       = "ui/icons/unlock.svg",
			Color      = "#5ac864",
			OnSelected = () => Release(),
		},
		new()
		{
			Label      = "Annuler",
			Icon       = "ui/icons/close.svg",
			Color      = "#ffffff",
			OnSelected = () => { },
		},
	};

	private void Handcuff()
	{

	}
	private void Search()
	{

	}
	private void Arrest()
	{

	}
	private void Release()
	{

	}
}
