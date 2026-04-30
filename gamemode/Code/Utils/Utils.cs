namespace OpenFramework.Utility;

public static class Utils
{
	public static Panel GetPanelInstance<T>() where T : Panel
	{
		return Game.ActiveScene.GetComponentsInChildren<PanelComponent>()
			.Select( x => x.Panel?.ChildrenOfType<T>().FirstOrDefault() )
			.FirstOrDefault( x => x != null );
	}

	public static List<T> GetPanelInstances<T>() where T : Panel
	{
		return Game.ActiveScene.GetComponentsInChildren<PanelComponent>()
			.Where( x => x.Panel != null )
			.SelectMany( x => x.Panel.ChildrenOfType<T>() )
			.ToList();
	}

	/// <summary>
	/// Get a component of type T from the active scene.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static T GetComponent<T>() where T : Component
	{
		return Game.ActiveScene.GetComponentInChildren<T>() ?? null;
	}

	/// <summary>
	/// Add a new component of type T to the active scene.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static T AddComponent<T>() where T : Component, new()
	{
		return Game.ActiveScene.AddComponent<T>() ?? null;
	}

	/// <summary>
	/// Create a new Panel instance of type T and add it to the active scene.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <returns></returns>
	public static T CreatePanelInstance<T>(string rootpanel = "hud") where T : Panel, new()
	{
		var panel = new T();
		Game.ActiveScene.GetComponentsInChildren<PanelComponent>().FirstOrDefault(x => x.GameObject.Name.Equals(rootpanel, StringComparison.OrdinalIgnoreCase)).Panel.AddChild( panel );
		return panel ?? null;
	}

	public static void AttachPanelToRoot( Panel panel, string rootpanel = "hud" )
	{
		Game.ActiveScene.GetComponentsInChildren<PanelComponent>().FirstOrDefault( x => x.GameObject.Name.Equals( rootpanel, StringComparison.OrdinalIgnoreCase ) ).Panel?.AddChild( panel );
	}

	/// <summary>
	/// Close and delete a Panel instance of type T from the active scene.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public static void ClosePanelByType<T>() where T : Panel
	{
		foreach ( var panel in GetPanelInstances<T>() )
			panel.Delete();
	}
}
