using Sandbox.Internal;
using OpenFramework.Systems.Pawn;
using OpenFramework.UI.RadialMenu;
using WorldInput = Sandbox.WorldInput;

namespace OpenFramework.Systems.RadialMenu;

//[Hide]
public abstract class RadialMenuBase : Component, IActionnable
{
	[Property, Sync( SyncFlags.FromHost )] public PlayerPawn Caller { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public PlayerPawn Target { get; set; }

	[Property] public virtual string HoldKey => "ActionMenu";
	[Property] public virtual GameObject DisplayPoint { get; set; }

	public abstract List<RadialMenuItem> BuildItems();

	private Sandbox.WorldPanel _worldPanel;
	private WorldInput _worldInput;
	private RadialMenuPanel _panel;

	public bool IsOpen => _panel != null;

	// ─────────────────────────────────────────────
	//  IActionnable
	// ─────────────────────────────────────────────

	public virtual UseResult CanAction( PlayerPawn player ) => true;

	public void OnAction( PlayerPawn player )
	{
		Open( player );
	}

	// ─────────────────────────────────────────────
	//  OPEN
	// ─────────────────────────────────────────────

	public void Open( PlayerPawn player )
	{
		if ( !Networking.IsHost ) return;

		Caller = player;

		var typeName = GetType().FullName;
		using ( Rpc.FilterInclude( player.Client.Connection ) )
			OpenOnClient( typeName );
	}

	[Rpc.Broadcast]
	private void OpenOnClient( string menuTypeName )
	{
		var menu = Scene.GetAllComponents<RadialMenuBase>()
			.FirstOrDefault( x => x.GetType().FullName == menuTypeName && x.GameObject == GameObject );

		if ( menu == null ) return;

		menu.OpenPanel();
	}

	// ─────────────────────────────────────────────
	//  PANEL LIFECYCLE
	// ─────────────────────────────────────────────

	private void OpenPanel()
	{
		ClosePanel();

		var items = BuildItems();
		if ( items == null || items.Count == 0 ) return;

		var spawnPoint = DisplayPoint != null ? DisplayPoint : GameObject;

		_worldPanel = spawnPoint.Components.Create<Sandbox.WorldPanel>();
		_worldPanel.RenderScale = 0.2f;
		_worldPanel.LookAtCamera = true;
		_worldPanel.InteractionRange = 200f;
		_worldPanel.PanelSize = 750f;
		_worldPanel.RenderOptions.Overlay = true;

		_worldInput = spawnPoint.Components.Create<WorldInput>();

		_panel = spawnPoint.Components.Create<RadialMenuPanel>();
		_panel.SetItems( items );
	}

	public void ClosePanel()
	{
		if ( !IsOpen ) return;

		_worldPanel.Destroy();
		_worldPanel = null;
		_worldInput.Destroy();
		_worldInput = null;
		_panel.Destroy();
		_panel = null;

		OnClose();
	}

	private void Confirm()
	{
		if ( !IsOpen || _panel == null ) return;

		var selected = _panel.GetSelected();
		ClosePanel();

		if ( selected != null )
		{
			OnItemSelected( selected );
			selected.OnSelected?.Invoke();
			Log.Info( selected.Label + " selected" );
		}
	}

	// ─────────────────────────────────────────────
	//  INPUT
	// ─────────────────────────────────────────────

	protected override void OnUpdate()
	{
		if ( IsProxy || !IsOpen ) return;

		/*var cam = Scene.Camera;
		if ( cam.IsValid() )
		{
			var dir = (_worldPanel.WorldPosition - cam.WorldPosition).Normal;
			_worldPanel.WorldRotation = Rotation.LookAt( dir, Vector3.Up );
		}*/

		if ( Input.Released( HoldKey ) )
		{
			if ( _panel?.GetSelected() != null )
				Confirm();
			else
				ClosePanel();
		}

		if ( Input.EscapePressed )
			ClosePanel();

	}

	// ─────────────────────────────────────────────
	//  HOOKS OPTIONNELS
	// ─────────────────────────────────────────────

	public virtual void OnClose() { }
	public virtual void OnItemSelected( RadialMenuItem item ) { }
}
