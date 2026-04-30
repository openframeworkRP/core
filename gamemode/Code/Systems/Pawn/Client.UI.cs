namespace OpenFramework.Systems.Pawn;

public partial class Client : Component
{
	[Rpc.Owner]
	public void AttachUI<T>() where T : Panel, new()
	{
		var panel = new T();
		Log.Info( $"AttachUI: {panel}" );

		AttachUI( panel);
	}

	[Rpc.Owner]
	public void AttachUI(Panel panel)
	{
		Log.Info( $"AttachUI: {panel}" );

		var rootpanel = Game.ActiveScene.GetComponentInChildren<PanelComponent>()?.Panel;

		if ( rootpanel != null )
			rootpanel.AddChild( panel );
	}
}
