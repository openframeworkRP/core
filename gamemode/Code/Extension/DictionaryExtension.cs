using System.Globalization;
namespace OpenFramework.Extension;

public static class DictionaryExtensions
{
	// ==========================================================
	// GETTERS (Récupération des données)
	// ==========================================================

	/// <summary>
	/// Récupère un entier. Retourne defaultValue si la clé n'existe pas ou est invalide.
	/// </summary>
	public static int GetInt( this IDictionary<string, string> dict, string key, int defaultValue = 0 )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && int.TryParse( val, out var result )
			? result : defaultValue;
	}

	public static int GetInt( this NetDictionary<string, string> dict, string key, int defaultValue = 0 )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && int.TryParse( val, out var result )
			? result : defaultValue;
	}

	/// <summary>
	/// Récupère un float (utilisé pour le poids, la soif, la faim, la durabilité).
	/// Utilise InvariantCulture pour ignorer les virgules/points selon la langue du PC.
	/// </summary>
	public static float GetFloat( this IDictionary<string, string> dict, string key, float defaultValue = 0f )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && float.TryParse( val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result )
			? result : defaultValue;
	}

	public static float GetFloat( this NetDictionary<string, string> dict, string key, float defaultValue = 0f )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && float.TryParse( val, NumberStyles.Any, CultureInfo.InvariantCulture, out var result )
			? result : defaultValue;
	}

	/// <summary>
	/// Récupère un booléen ("True" ou "False").
	/// </summary>
	public static bool GetBool( this IDictionary<string, string> dict, string key, bool defaultValue = false )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && bool.TryParse( val, out var result )
			? result : defaultValue;
	}

	public static bool GetBool( this NetDictionary<string, string> dict, string key, bool defaultValue = false )
	{
		return dict != null && dict.TryGetValue( key, out var val ) && bool.TryParse( val, out var result )
			? result : defaultValue;
	}

	// ==========================================================
	// SETTERS (Enregistrement des données)
	// ==========================================================

	/// <summary>
	/// Enregistre un entier sous forme de texte.
	/// </summary>
	public static void SetInt( this IDictionary<string, string> dict, string key, int value )
	{
		if ( dict == null ) return;
		dict[key] = value.ToString();
	}

	public static void SetInt( this NetDictionary<string, string> dict, string key, int value )
	{
		if ( dict == null ) return;
		dict[key] = value.ToString();
	}

	/// <summary>
	/// Enregistre un float en forçant le point "." comme séparateur décimal.
	/// </summary>
	public static void SetFloat( this IDictionary<string, string> dict, string key, float value )
	{
		if ( dict == null ) return;
		dict[key] = value.ToString( CultureInfo.InvariantCulture );
	}

	public static void SetFloat( this NetDictionary<string, string> dict, string key, float value )
	{
		if ( dict == null ) return;
		dict[key] = value.ToString( CultureInfo.InvariantCulture );
	}

	/// <summary>
	/// Enregistre un booléen.
	/// </summary>
	public static void SetBool( this IDictionary<string, string> dict, string key, bool value )
	{
		if ( dict == null ) return;
		dict[key] = value.ToString();
	}

	/// <summary>
	/// Ajoute ou met à jour une valeur string.
	/// </summary>
	public static void SetString( this IDictionary<string, string> dict, string key, string value )
	{
		if ( dict == null ) return;
		dict[key] = value;
	}
}
