using OpenFramework.Inventory;

namespace OpenFramework.UI.QuickMenuSystem;

/// <summary>
///	Menu for interacting with an item in the game world.
/// </summary>
/// <param name="item"></param>
public record ItemInteractMenu( WorldItem world ) : IQuickMenuInterface
{
	// On récupère les infos depuis la Metadata liée au WorldItem
	public string Title => world.Item?.Name ?? "Objet inconnu";
	//public string SubTitle => item.Metadata?.Description ?? "";
	public string SubTitle => "😎";
	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();
		var item = world?.Item;

		if ( item?.Metadata == null )
			return list;

		// Action "Utiliser" pour les consommables
		if ( item.Metadata.OnUseAction != null && item.Metadata.IsConsumable )
		{
			list.Add( new MenuItem( "Utiliser", () =>
			{
				world.Host_UseDirectly();
			}, GoBackOnSelect: true ) );
		}

		return list;
	}
}
