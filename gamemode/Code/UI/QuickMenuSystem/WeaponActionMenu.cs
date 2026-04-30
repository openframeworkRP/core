using Facepunch;
using OpenFramework.Command;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using static Facepunch.NotificationSystem;

namespace OpenFramework.UI.QuickMenuSystem;

public record WeaponActionMenu(DroppedEquipment equipment) : IQuickMenuInterface
{
	public string Title => equipment.GameObject.Name ?? "Unknown weapon";
	public string SubTitle => "";
	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var item = ItemMetadata.All.Where( x => x.IsWeapon && x.WeaponResource == equipment.Resource ).FirstOrDefault();
		if ( item == null )
		{
			QuickMenu.Close();
			return null;
		}

		var list = new List<MenuItem>();

		list.Add( new MenuItem( "Ranger", () =>
		{
			if ( equipment.GameObject == null )
			{
				QuickMenu.Close();
				return;
			}

			var containerRef = Client.Local?.PlayerPawn?.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
			//InventoryContainer.HostAdd( containerRef, item.ResourceName );
			Commands.RPC_GiveItem(Client.Local, item.ResourceName, 1);	
			Spawnable.Destroy(equipment.GameObject);
			Client.Local.Notify( NotificationType.Success, $"Vous avez ranger {equipment.GameObject.Name} dans votre inventaire" );
		}, CloseMenuOnSelect: true ) );

		return list;
	}
}
