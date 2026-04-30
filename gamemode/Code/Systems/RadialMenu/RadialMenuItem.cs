namespace OpenFramework.UI.RadialMenu;

/// <summary>
/// Représente une entrée dans le menu radial.
/// </summary>
public class RadialMenuItem
{
	/// <summary>Label affiché dans le menu.</summary>
	public string Label { get; set; }

	/// <summary>Icône SVG (chemin vers le fichier svg).</summary>
	public string Icon { get; set; }

	/// <summary>Couleur d'accentuation de l'item.</summary>
	public string Color { get; set; } = "#ffffff";

	/// <summary>Si false, l'item est affiché mais non sélectionnable.</summary>
	public bool IsEnabled { get; set; } = true;

	/// <summary>Action exécutée quand l'item est sélectionné.</summary>
	public Action OnSelected { get; set; }
}
