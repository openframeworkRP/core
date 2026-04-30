using OpenFramework.UI.RadialMenu;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial de test — place ce component sur n'importe quel GameObject
/// pour vérifier que le système fonctionne.
///
/// Setup :
///   1. Crée un GameObject dans la scène
///   2. Ajoute un WorldPanel + BoxCollider (trigger) avec le tag "use"
///   3. Ajoute ce component
///   4. Appuie sur E en regardant l'objet
/// </summary>
public class TestRadialMenu : RadialMenuBase
{
	public override List<RadialMenuItem> BuildItems() => new()
	{
		new()
		{
			Label      = "Rouler",
			Icon       = "ui/icons/use.svg",
			Color      = "#5ac864",
			OnSelected = () => Log.Info( $"[TestRadialMenu] Action 1 — Caller: {Caller?.DisplayName}" ),
		},
		new()
		{
			Label      = "Ranger",
			Icon       = "ui/icons/search.svg",
			Color      = "#5ac864",
			OnSelected = () => Log.Info( $"[TestRadialMenu] Action 2 — Caller: {Caller?.DisplayName}" ),
		},
		new()
		{
			Label      = "Désactivé",
			Icon       = "ui/icons/close.svg",
			Color      = "#5ac864",
			//IsEnabled  = false,
			OnSelected = () => Log.Info( "[TestRadialMenu] Tu ne devrais pas voir ça" ),
		}
	};

	public override void OnItemSelected( RadialMenuItem item )
	{
		Log.Info( $"[TestRadialMenu] Sélectionné : {item.Label}" );
	}

	public override void OnClose()
	{
		Log.Info( "[TestRadialMenu] Menu fermé" );
	}
}
