using OpenFramework.UI.RadialMenu;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial du médecin — affiché quand il interagit avec un joueur blessé.
/// </summary>
public class MedicRadialMenu : RadialMenuBase
{
	public override List<RadialMenuItem> BuildItems() => new()
	{
		new()
		{
			Label      = "Soigner",
			Icon       = "ui/icons/medkit.svg",
			Color      = "#5ac864",
			//IsEnabled  = Target != null,
			OnSelected = () => HealTarget(),
		},
		new()
		{
			Label      = "Ranimer",
			Icon       = "ui/icons/heart.svg",
			Color      = "#e25050",
			//IsEnabled  = Target != null,
			OnSelected = () => ReviveTarget(),
		},
		new()
		{
			Label      = "Inspecter",
			Icon       = "ui/icons/search.svg",
			Color      = "#007AFF",
			OnSelected = () => InspectTarget(),
		},
		new()
		{
			Label      = "Annuler",
			Icon       = "ui/icons/close.svg",
			Color      = "#ffffff",
			OnSelected = () => { },
		},
	};

	private void HealTarget()
	{

	}

	private void ReviveTarget()
	{

	}

	private void InspectTarget()
	{

	}
}
