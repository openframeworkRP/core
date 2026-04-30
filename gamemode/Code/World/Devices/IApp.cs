// /Apps/IApp.cs
using OpenFramework.Systems.Jobs;

namespace OpenFramework.World.Devices;

public interface IApp
{
	string AppId { get; }
	string DisplayName { get; }
	string Icon { get; }
	string IconColor { get; }
	string BackgroundColor { get; }
	bool IsSystemApp { get; }
	bool IsPinned { get; }

	JobList JobAccess { get; }
	Type SplashScreen { get; }
	DeviceKind Compatibility { get; }

	void Open( AppContext ctx );
	void Close();
	void OnOpen( AppContext ctx );
	void OnClose();
}

public record RegisteredApp(string AppId, string DisplayName, string Icon);

// Contexte offert aux apps (services & device)
public record AppContext
{
	public IDevice Device { get; }
	public Panel Container { get; set; }

	public AppContext( IDevice device, Panel container )
	{
		Device = device;
		Container = container;
	}
}
