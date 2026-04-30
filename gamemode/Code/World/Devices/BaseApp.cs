using OpenFramework.Systems.Jobs;

namespace OpenFramework.World.Devices;

public abstract class BaseApp : Panel, IApp
{
	public abstract string AppId { get; }

	public abstract string DisplayName { get; }

	public abstract string Icon { get; }

	public abstract string IconColor { get; }
	public abstract string BackgroundColor { get; }
	public abstract bool IsSystemApp { get; }
	public bool IsPinned { get; set; }

	public abstract JobList JobAccess { get; }

	public abstract Type SplashScreen { get; }

	public abstract DeviceKind Compatibility { get; }

	public AppContext AppContext { get; set; }

	public virtual bool IsHidden => false;

	protected BaseApp()
	{
		StyleSheet.Load( "World/Devices/BaseApp.cs.scss" );
		AddClass( "baseapp" );
	}

	public virtual void Open(AppContext ctx)
	{
		OnOpen( ctx );

		var panel = TypeLibrary.Create<BaseAppSplashScreen>( SplashScreen );
		panel.Context = ctx;
		AddPanelDynamicStyles(panel, ctx.Device.Kind );

		ctx.Container.AddChild( panel );
	}

	public virtual void Close()
	{
		AppContext.Device.CurrentApp = null;
		OnClose();
		Delete();
	}

	public static void AddPanelDynamicStyles( Panel panel, DeviceKind type )
	{
		// On s'assure que le contenu de l'app est bien découpé par les arrondis
		panel.Style.Overflow = OverflowMode.Hidden;

		switch ( type )
		{
			case DeviceKind.Phone:
				panel.AddClass( "phoneapp" );
				break;

			case DeviceKind.Tablet:
				panel.AddClass( "tabletapp" );
				break;

			case DeviceKind.Computer:
				panel.AddClass( "computerapp" );
				break;
		}
	}

	public virtual void OnClose()
	{
		//throw new NotImplementedException();
		Delete();
	}

	public virtual void OnOpen( AppContext ctx )
	{
		//throw new NotImplementedException();
	}

	public List<BaseAppSettings> GetSettings()
	{
		return DevicesManager.Instance.Settings
			.Where( x => x is BaseAppSettings )
			.Cast<BaseAppSettings>()
			.Where( x => x.Id.StartsWith( AppId + "_" ) )
			.ToList();
	}
}
