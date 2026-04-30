using System.Text.Json.Serialization;

namespace OpenFramework.World.Devices;

/// <summary>
/// Manages all devices and their persisted settings.
/// Settings are stored via FileSystem.Data (client-side JSON file).
/// </summary>
public class DevicesManager : SingletonComponent<DevicesManager>
{
	[JsonIgnore] public List<IApp> LoadedApps { get; } = new List<IApp>();
	[JsonIgnore] public List<BaseSettingsDefinition> Settings { get; private set; } = new List<BaseSettingsDefinition>();

	private const string SettingsFilePath = "devicesystem_settings.json";

	// Valeurs sauvegardées au dernier chargement (Id -> Value)
	private Dictionary<string, string> _savedValues = new();

	protected override void OnAwake()
	{
		base.OnAwake();

		// 1. Charger les valeurs persistées AVANT d'instancier les apps
		LoadSavedValues();

		// 2. Instancier toutes les apps (leurs constructeurs appellent RegisterSetting)
		foreach ( var type in TypeLibrary.GetTypes()
			.Where( x => !x.IsAbstract && typeof( IApp ).IsAssignableFrom( x.TargetType ) ) )
		{
			var instance = (IApp)TypeLibrary.Create( type.Name, type.TargetType );
			if ( instance != null )
				LoadedApps.Add( instance );
		}

		// 3. Réglages système
		AddSystemSettings();
	}

	protected override void OnDestroy()
	{
		SaveSettings();
	}

	// ─────────────────────────────────────────────────────────────
	//  REGISTRATION
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Enregistre un réglage. Si une valeur sauvegardée existe pour cet Id, elle est restaurée.
	/// </summary>
	public static void RegisterSetting( BaseSettingsDefinition definition )
	{
		if ( Instance == null || definition == null ) return;

		if ( Instance.Settings.Any( x => x.Id == definition.Id ) )
		{
			Log.Warning( $"[Settings] Doublon ignoré : {definition.Id}" );
			return;
		}

		// Restaurer la valeur sauvegardée si disponible
		if ( Instance._savedValues.TryGetValue( definition.Id, out var savedValue ) )
		{
			definition.Value = savedValue;
		}

		Instance.Settings.Add( definition );
	}

	// ─────────────────────────────────────────────────────────────
	//  PERSISTENCE  (FileSystem.Data = client-side, persiste entre sessions)
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Charge les valeurs sauvegardées depuis le fichier JSON.
	/// On ne charge QUE les valeurs (Id+Value) pour éviter les conflits de désérialisation
	/// avec les actions non-sérialisables.
	/// </summary>
	private void LoadSavedValues()
	{
		_savedValues = new Dictionary<string, string>();

		if ( !FileSystem.Data.FileExists( SettingsFilePath ) )
			return;

		try
		{
			var saved = FileSystem.Data.ReadJson<List<SettingsSaveEntry>>( SettingsFilePath );
			if ( saved != null )
			{
				foreach ( var entry in saved )
				{
					if ( entry.Id != null )
						_savedValues[entry.Id] = entry.Value ?? "";
				}
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Settings] Erreur de chargement : {e.Message}" );
		}
	}

	/// <summary>
	/// Sauvegarde uniquement les Id+Value dans le fichier JSON.
	/// </summary>
	public void SaveSettings()
	{
		var entries = Settings.Select( s => new SettingsSaveEntry
		{
			Id = s.Id,
			Value = s.Value
		} ).ToList();

		FileSystem.Data.WriteJson( SettingsFilePath, entries );
		Log.Info( $"[Settings] {entries.Count} réglage(s) sauvegardé(s)." );
	}

	// ─────────────────────────────────────────────────────────────
	//  RÉGLAGES SYSTÈME
	// ─────────────────────────────────────────────────────────────

	private void AddSystemSettings()
	{
		RegisterSetting( new BaseDeviceSettings
		{
			Id = "airplane_mode",
			Label = "Mode Avion",
			Category = "Connectivité",
			Value = "Désactivé",
			Icon = "ui/icons/airplane.svg",
			IconColor = "#FF9500",
			OnChanged = ( val ) => Log.Info( $"[Settings] Mode Avion -> {val}" )
		} );

		RegisterSetting( new BaseDeviceSettings
		{
			Id = "wifi_enabled",
			Label = "Wi-Fi",
			Category = "Connectivité",
			Value = "Activé",
			Icon = "ui/icons/wifi.svg",
			IconColor = "#007AFF",
			OnChanged = ( val ) => Log.Info( $"[Settings] Wi-Fi -> {val}" )
		} );

		RegisterSetting( new BaseDeviceSettings
		{
			Id = "bluetooth_enabled",
			Label = "Bluetooth",
			Category = "Connectivité",
			Value = "Activé",
			Icon = "ui/icons/bluetooth.svg",
			IconColor = "#007AFF",
			OnChanged = ( val ) => Log.Info( $"[Settings] Bluetooth -> {val}" )
		} );

		RegisterSetting( new BaseDeviceSettings
		{
			Id = "notifications_enabled",
			Label = "Notifications",
			Category = "Notifications",
			Value = "Activé",
			Icon = "ui/icons/bell.svg",
			IconColor = "#FF3B30",
			OnChanged = ( val ) => Log.Info( $"[Settings] Notifications -> {val}" )
		} );

		RegisterSetting( new BaseDeviceSettings
		{
			Id = "dnd_mode",
			Label = "Ne pas déranger",
			Category = "Notifications",
			Value = "Désactivé",
			Icon = "ui/icons/moon.svg",
			IconColor = "#5856D6",
			OnChanged = ( val ) => Log.Info( $"[Settings] DnD -> {val}" )
		} );
	}

	// ─────────────────────────────────────────────────────────────
	//  UTILITAIRES
	// ─────────────────────────────────────────────────────────────

	/// <summary>
	/// Récupère la valeur d'un réglage par son Id.
	/// </summary>
	public static string GetValue( string id, string defaultValue = "" )
	{
		if ( Instance == null ) return defaultValue;
		return Instance.Settings.FirstOrDefault( s => s.Id == id )?.Value ?? defaultValue;
	}

	/// <summary>
	/// Définit la valeur d'un réglage par son Id et déclenche le callback.
	/// </summary>
	public static void SetValue( string id, string value )
	{
		if ( Instance == null ) return;
		var setting = Instance.Settings.FirstOrDefault( s => s.Id == id );
		if ( setting == null ) return;
		setting.Value = value;
		setting.OnChanged?.Invoke( value );
		Instance.SaveSettings();
	}
}

/// <summary>
/// DTO minimal pour la sérialisation des réglages (Id + Value uniquement).
/// </summary>
public class SettingsSaveEntry
{
	public string Id { get; set; }
	public string Value { get; set; }
}
