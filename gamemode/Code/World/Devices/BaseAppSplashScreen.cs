using OpenFramework.World.Devices.Shared;

namespace OpenFramework.World.Devices;

public abstract class BaseAppSplashScreen : Panel
{
	internal RealTimeUntil UntilClose = 5f;
	public abstract Type AppInterface { get; }
	public AppContext Context { get; set; }

	public BaseAppSplashScreen()
	{
		StyleSheet.Load( "World/Devices/BaseAppSplashScreen.cs.scss" );
		AddClass( "splashscreen" );
	}

	public override void Tick()
	{
		if ( UntilClose )
		{
			var panel = TypeLibrary.Create<BaseApp>( AppInterface );
			panel.AppContext = Context;
			panel.AddClass( "baseapp" );
			BaseApp.AddPanelDynamicStyles( panel, Context.Device.Kind );
			panel.AddChild( new CloseButton( panel, Context ) );

			Parent.AddChild( panel );
			Delete();
		}
	}
}
