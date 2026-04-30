using OpenFramework.Command;
using OpenFramework.Inventory;
using static Facepunch.NotificationSystem;

namespace OpenFramework.UI.QuickMenuSystem;

public record ClothingActionMenu( ClothingContainer.ClothingEntry Clothing) : IQuickMenuInterface
{
	public string Title => Clothing.Clothing.Title ?? "Unknown clothing";
	public string SubTitle => "";
	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var clothing = Clothing.Clothing;

		Log.Info( $"Clothing Title: {clothing.Title}" );
		Log.Info( $"Clothing ResourceName: {clothing.ResourceName}" );

		var item = ItemMetadata.All.FirstOrDefault( x =>
			x.IsClothing &&
			x.ResourceName == clothing.ResourceName
		);

		if ( item == null )
		{
			Log.Warning( $"Aucun item trouvé pour {clothing.ResourceName}" );
			QuickMenu.Close();
			return null;
		}

		var list = new List<MenuItem>();

		list.Add( new MenuItem( "Ranger", () =>
		{
			var containerRef = Client.Local?.PlayerPawn?
				.GameObject
				.Components
				.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

			Commands.RPC_GiveItem( Client.Local, item.ResourceName, 1 );

			Client.Local.Notify(
				NotificationType.Success,
				$"Vous avez rangé {clothing.Title} dans votre inventaire"
			);
		}, CloseMenuOnSelect: true ) );

		return list;
	}

}
