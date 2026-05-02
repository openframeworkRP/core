namespace OpenFramework.Systems.Gps;

/// <summary>
/// Gère l'état "minimap visible via item GPS" — purement client-side.
/// Chaque client a son propre état indépendant, pas de synchronisation réseau.
/// </summary>
public static class GpsItemSystem
{
	/// <summary>True si le joueur a activé sa minimap via l'item GPS.</summary>
	public static bool IsMinimapEnabled { get; private set; }

	/// <summary>Bascule l'état actif/inactif de la minimap GPS.</summary>
	public static void Toggle() => IsMinimapEnabled = !IsMinimapEnabled;

	/// <summary>Force la désactivation (appelé quand le joueur n'a plus l'item).</summary>
	public static void Disable() => IsMinimapEnabled = false;
}
