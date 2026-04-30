using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.UI.RadialMenu;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial affiché quand un joueur interagit avec un WorldItem.
/// Remplace ItemInteractMenu (IQuickMenuInterface).
/// À placer sur le même GameObject que le WorldItem.
/// </summary>
public class WorldItemRadialMenu : RadialMenuBase
{
	[Property] public InventoryItem Item => GameObject.GetComponentInChildren<InventoryItem>();

	public override List<RadialMenuItem> BuildItems()
	{
		var list = new List<RadialMenuItem>();

		if ( Item?.Metadata == null ) return list;

		// ── Utiliser ──────────────────────────────
		if ( Item.Metadata.OnUseAction != null && Item.Metadata.IsConsumable )
		{
			list.Add( new RadialMenuItem
			{
				Label = "Utiliser",
				Icon = "ui/icons/use.svg",
				Color = "#5ac864",
				OnSelected = () => Host_UseDirectly(),
			} );
		}

		list.Add( new RadialMenuItem
		{
			Label = "Ramasser",
			Icon = "ui/icons/backpack.svg",
			Color = "#5ac864",
			OnSelected = () => Host_PickUp( ),
		} );

		return list;
	}

	[Rpc.Host]
	public void Host_PickUp()
	{
		var caller = Rpc.Caller.GetClient();
		var pawn = caller?.PlayerPawn;
		var constants = Constants.Instance;

		if ( pawn == null || Item == null ) return;

		var inventory = pawn.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( inventory == null ) return;

		if ( constants.Debug )
			Log.Info( $"[WorldItem] Get InventoryContainer: {inventory}" );

		int freeSlot = inventory.GetFirstFreeSlot();
		if ( freeSlot == -1 ) return;

		var itemGo = Item.GameObject;
		itemGo.Parent = inventory.GameObject;
		Item.SlotIndex = freeSlot;

		if ( constants.Debug )
			Log.Info( $"[WorldItem] Host_PickUp: {itemGo} successfully" );

		GameObject.Destroy();
	}

	[Rpc.Host]
	public void Host_UseDirectly()
	{
		if ( Item == null ) return;

		var caller = Rpc.Caller.GetClient();
		Log.Info( $"[RadialMenu] Host_UseDirectly — caller={caller?.DisplayName ?? "NULL"}, item={Item?.Name}" );

		InventoryContainer.Use( Item );

		if ( Item == null || !Item.IsValid ) GameObject.Destroy();
	}
}
