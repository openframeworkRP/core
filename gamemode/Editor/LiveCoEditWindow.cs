// editor/LiveCoEditWindow.cs
using Editor;
using Sandbox;
using System;

[Dock( "Editor", "Live Co-Edit", "groups" )]
public class LiveCoEditWindow : Widget
{
	LineEdit _serverUrl;
	LineEdit _room;
	Label _status;
	Button _connectBtn;

	//LiveCoEditClient _client; // ta classe WebSocket de l'autre message

	public LiveCoEditWindow( Widget parent = null ) : base( parent )
	{
		WindowTitle = "Live Co-Edit";
		SetWindowIcon( "groups" );

		Layout = Layout.Column();
		Layout.Margin = 8;
		Layout.Spacing = 6;

		// --- Form ---
		_serverUrl = Layout.Add( new LineEdit( this ) { PlaceholderText = "ws://relay.tld:8080" } );
		_room = Layout.Add( new LineEdit( this ) { PlaceholderText = "room id (ex: myproject)" } );

		_connectBtn = Layout.Add( new Button( "Connect", this ) );
		_connectBtn.Clicked = OnConnectClicked;

		_status = Layout.Add( new Label( "Disconnected", this ) );
		_status.SetStyles( "color: #888;" );

		Layout.AddStretchCell();

		// --- Peers list ---
		var peersHeader = Layout.Add( new Label( "Peers", this ) );
		peersHeader.SetStyles( "font-weight: 600; margin-top: 8px;" );
		// ListView de peers connectés…
	}

	void OnConnectClicked()
	{
		/*if ( _client?.IsConnected == true )
		{
			_client.Disconnect();
			SetStatus( "Disconnected", "#888" );
			_connectBtn.Text = "Connect";
			return;
		}

		_client = new LiveCoEditClient();
		_client.OnStatus = ( s, color ) => SetStatus( s, color );
		_ = _client.Connect( _serverUrl.Text, _room.Text );
		_connectBtn.Text = "Disconnect";*/
	}

	void SetStatus( string text, string color )
	{
		_status.Text = text;
		_status.SetStyles( $"color: {color};" );
	}

	// Hot-reload friendly : auto-reconnect après recompile
	[EditorEvent.Hotload]
	void OnHotload()
	{
		/*if ( _client?.IsConnected == true )
			SetStatus( "Reconnecting after hotload…", "#cc0" );*/
	}
}
