using OpenFramework.GameLoop;
using OpenFramework.UI.QuickMenuSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	/// <summary>
	/// DEBUG : ouvre le PlayerRadialMenu directement en mode liste avec les amendes de Constants.
	/// Permet d'iterer sur le CSS de .list-bg sans avoir a passer par le flow police + cible.
	/// </summary>
	[Command( "Debug Fines Menu", ["debugfines"], "Ouvre le sous-menu amendes en mode liste pour ajuster le CSS", "ui/icons/admin.svg", CommandPermission.Admin )]
	public static void DebugFinesMenu()
	{
		var reasons = Constants.Instance?.FineReasons;
		if ( reasons == null || reasons.Count == 0 )
		{
			Log.Warning( "[DebugFines] Aucune FineReason configuree dans Constants." );
			return;
		}

		var items = reasons
			.Select( r => ($"{r.Name} ({r.Amount}$)", "/ui/icons/work.svg"))
			.ToList();

		PlayerRadialMenu.OpenDebugFines( items );
	}
}
