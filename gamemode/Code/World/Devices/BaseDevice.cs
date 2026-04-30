using OpenFramework.Utility;

namespace OpenFramework.World.Devices;

public enum DeviceKind
{
	TV,
	Phone,
	Computer,
	Tablet,
	Console,
	Factory,
	All
}

[Flags]
public enum DeviceCapabilities
{
	None = 0,
	Touch = 1 << 0,
	Keyboard = 1 << 1,
	Pointer = 1 << 2,
	Speakers = 1 << 3,
	Microphone = 1 << 4,
	Camera = 1 << 5,
	GPS = 1 << 6,
	Network = 1 << 7,
	Bluetooth = 1 << 8,
	Cellular = 1 << 9,
	// Facile à étendre plus tard
}

public interface IDevice
{
	public interface IScreenContext
	{
		IDevice AttachedDevice { get; set; }
	}

	public interface IBatteryProvider
	{
		float BatteryLevel { get; }
		bool IsCharging { get; }
	}

	public interface INetworkProvider
	{
		string SSID { get; }
		int SignalStrength { get; }
	}

	/// <summary>
	/// Type of device
	/// </summary>
	DeviceKind Kind { get; }

	/// <summary>
	/// 
	/// </summary>
	PanelComponent ScreenUI { get; set; }
	DeviceCapabilities Capabilities { get; }

	/// App courante (si ouverte)
	IApp CurrentApp { get; set; }
	List<IApp> OpenedApps { get; }
	float SwipeOffset { get; }
	bool IsOn { get; }

	/// Ouvre/ferme une app (le shell gère l’UI)
	void OpenApp( IApp app, Panel container );

	/// Hook alimentation / power
	void PowerOn();
	void PowerOff();
}

public class BaseDevice : Component, IDevice, IDevice.IBatteryProvider
{
	// ── Identité ─────────────────────────────────────────────────
	[Property] public DeviceKind Kind { get; set; }
	[Property] public PanelComponent ScreenUI { get; set; }

	// ── IBatteryProvider ─────────────────────────────────────────
	[Property] public float BatteryLevel { get; set; } = 100f;
	[Property] public bool IsCharging { get; set; }

	// ── Capabilities ─────────────────────────────────────────────
	[Property] public DeviceCapabilities Capabilities { get; set; }

	// ── Settings ─────────────────────────────────────────────────
	[Property] public List<BaseSettingsDefinition> Settings { get; set; } = new();

	// ── Runtime ──────────────────────────────────────────────────
	public List<IApp> OpenedApps { get; protected set; } = new();
	public IApp CurrentApp { get; set; }
	public float SwipeOffset { get; set; } = 0f;
	public bool IsOn { get; set; } = false;

	// ── App management ───────────────────────────────────────────

	protected override void OnStart()
	{
		foreach ( var setting in Settings )
			DevicesManager.RegisterSetting( setting );
	}

	public virtual void OpenApp( IApp app, Panel container )
	{
		if ( app == null ) return;

		if ( !OpenedApps.Contains( app ) )
			OpenedApps.Add( app );

		CurrentApp = app;
		app.Open( new AppContext( this, container ) );
	}

	public virtual void CloseApp( IApp app )
	{
		if ( !OpenedApps.Contains( app ) ) return;

		OpenedApps.Remove( app );
		app.Close();

		if ( CurrentApp == app )
			CurrentApp = null;
	}

	public virtual void PowerOn() 
	{
		ScreenUI.Enabled = true;
		ScreenUI.Panel.Focus();
		IsOn = true;
	}

	public virtual async void PowerOff() 
	{
		ScreenUI.Panel.Delete();
		await GameTask.Delay( 200 );

		ScreenUI.Enabled = false;
		IsOn = false;
	}

	public List<BaseDeviceSettings> GetDeviceSettings()
	{
		return DevicesManager.Instance.Settings
			.OfType<BaseDeviceSettings>()
			.Where( x => x.DeviceType == Kind )
			.ToList();
	}
}
