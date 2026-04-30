using Sandbox;
using System.Linq;

namespace OpenFramework.Utility;

/// <summary>
/// Helper de diagnostic UI. Enregistre la commande console "ui_dump".
/// Usage: ui_dump creator   puis   ui_dump ingame
/// </summary>
public static class UiStateDebugger
{
	[ConCmd( "ui_dump" )]
	public static void UiDump( string label = "" )
	{
		var scene = Game.ActiveScene;
		if ( scene == null )
		{
			Log.Warning( "[UI-STATE] Game.ActiveScene est null" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( label ) ) label = "(no-label)";

		Log.Info( $"========== [UI-STATE] ui_dump ({label}) ==========" );
		Log.Info( $"[UI-STATE] Scene            = {scene.Name}" );
		Log.Info( $"[UI-STATE] Mouse.Visibility = {Mouse.Visibility}" );
		Log.Info( $"[UI-STATE] Mouse.Position   = {Mouse.Position}" );
		Log.Info( $"[UI-STATE] Screen.Size      = {Screen.Size}" );

		try
		{
			var cameras = scene.GetAllComponents<CameraComponent>().ToList();
			Log.Info( $"[UI-STATE] -- Cameras ({cameras.Count}) --" );
			foreach ( var c in cameras )
			{
				if ( c == null || !c.IsValid() ) continue;
				Log.Info( $"[UI-STATE]   cam go={c.GameObject?.Name} enabled={c.Enabled} isMain={c.IsMainCamera} prio={c.Priority} pp={c.EnablePostProcessing}" );
			}
		}
		catch ( System.Exception ex ) { Log.Warning( $"[UI-STATE] Cameras: {ex.Message}" ); }

		try
		{
			var panels = scene.GetAllComponents<ScreenPanel>().ToList();
			Log.Info( $"[UI-STATE] -- ScreenPanels ({panels.Count}) --" );
			foreach ( var p in panels )
			{
				if ( p == null || !p.IsValid() ) continue;
				Log.Info( $"[UI-STATE]   sp go={p.GameObject?.Name} enabled={p.Enabled} z={p.ZIndex} opacity={p.Opacity} scale={p.Scale}" );
			}
		}
		catch ( System.Exception ex ) { Log.Warning( $"[UI-STATE] ScreenPanels: {ex.Message}" ); }

		try
		{
			var world = scene.GetAllComponents<Sandbox.WorldPanel>().ToList();
			Log.Info( $"[UI-STATE] -- WorldPanels ({world.Count}) --" );
			foreach ( var w in world )
			{
				if ( w == null || !w.IsValid() ) continue;
				Log.Info( $"[UI-STATE]   wp go={w.GameObject?.Name} enabled={w.Enabled} size={w.PanelSize}" );
			}
		}
		catch ( System.Exception ex ) { Log.Warning( $"[UI-STATE] WorldPanels: {ex.Message}" ); }

		try
		{
			var pcs = scene.GetAllComponents<PanelComponent>().ToList();
			Log.Info( $"[UI-STATE] -- PanelComponents ({pcs.Count}) --" );
			foreach ( var pc in pcs )
			{
				if ( pc == null || !pc.IsValid() ) continue;
				Log.Info( $"[UI-STATE]   pc {pc.GetType().Name} go={pc.GameObject?.Name} enabled={pc.Enabled}" );
			}
		}
		catch ( System.Exception ex ) { Log.Warning( $"[UI-STATE] PanelComponents: {ex.Message}" ); }

		Log.Info( $"========== [UI-STATE] ui_dump end ({label}) ==========" );
	}
}
