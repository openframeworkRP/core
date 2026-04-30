using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.UI.QuickMenuSystem;

namespace OpenFramework.Inventory;

public class WorldItem : Component//, IUse
{
	/// <summary>
	/// Récupère le composant de données situé dans le prefab.
	/// </summary>
	public InventoryItem Item => GameObject.GetComponentInChildren<InventoryItem>();

	/*public UseResult CanUse( PlayerPawn player )
	{
		return true;
	}

	/// <summary>
	/// ÉTAPE 1 : Le joueur intéragit avec l'objet.
	/// Si l'item est uniquement ramassable, le hold E gère le pickup directement.
	/// Sinon (consommable, etc.), on ouvre le QuickMenu.
	/// </summary>
	public void OnUse( PlayerPawn player )
	{
		// Le ramassage est géré par le hold E dans PlayerPawn.UpdateUse, pas ici
		var item = Item;
		if ( item?.Metadata == null ) return;

		// Ouvrir le QuickMenu uniquement si l'item a des actions spéciales (consommable, etc.)
		if ( !item.Metadata.IsConsumable ) return;

		using ( Rpc.FilterInclude( player.Client.Connection ) )
		{
			QuickMenu.OpenItemInteraction( this );
		}
	}*/

	/// <summary>
	/// ÉTAPE 2 : Appelée depuis le UI/Menu après que le joueur a cliqué sur "Ramasser".
	/// </summary>
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

		// Transfert du GameObject de l'item vers l'inventaire
		var itemGo = Item.GameObject;
		itemGo.Parent = inventory.GameObject;
		Item.SlotIndex = freeSlot;

		if ( constants.Debug )
			Log.Info( $"[WorldItem] Host_PickUp: {itemGo} successfully" );

		// Destruction de l'enveloppe physique au sol
		GameObject.Destroy();
	}

	/// <summary>
	/// Utilisation directe depuis le menu (ex: "Manger" sans ramasser).
	/// </summary>
	[Rpc.Host]
	public void Host_UseDirectly()
	{
		if ( Item == null ) return;

		var caller = Rpc.Caller.GetClient();
		Log.Info( $"[WorldItem] Host_UseDirectly — caller={caller?.DisplayName ?? "NULL"}, item={Item?.Name}" );

		InventoryContainer.Use( Item );

		if ( Item == null || !Item.IsValid ) GameObject.Destroy();
	}
}
