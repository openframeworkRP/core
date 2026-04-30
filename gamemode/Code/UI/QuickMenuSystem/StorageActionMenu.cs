using OpenFramework.Inventory;
using OpenFramework.UI.World.Storage;
using System.Threading.Tasks;

namespace OpenFramework.UI.QuickMenuSystem;

public record StorageActionMenu( StorageComponent storage ) : IQuickMenuInterface
{
	public string Title => storage.GameObject.Name ?? "Coffre";
	public string SubTitle => storage.IsLocked
		? ( storage.HasCode ? "Verrouille" : "Sans code" )
		: "Deverrouille";
	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();

		bool isOwner = storage.Owner == Client.Local;

		if ( isOwner )
		{
			if ( !storage.HasCode )
			{
				// Le coffre vient d'etre pose : l'owner doit d'abord definir un code.
				list.Add( new MenuItem( "Definir le code", () =>
				{
					StorageCodePanel.Open( storage, StorageCodePanel.Mode.Define );
				}, CloseMenuOnSelect: true ) );
			}
			else if ( storage.IsLocked )
			{
				list.Add( new MenuItem( "Entrer le code", () =>
				{
					StorageCodePanel.Open( storage, StorageCodePanel.Mode.Unlock );
				}, CloseMenuOnSelect: true ) );

				list.Add( new MenuItem( "Changer le code", () =>
				{
					StorageCodePanel.Open( storage, StorageCodePanel.Mode.Define );
				}, CloseMenuOnSelect: true ) );
			}
			else
			{
				list.Add( new MenuItem( "Verrouiller le coffre", () =>
				{
					StorageComponent.Lock( storage );
				}, CloseMenuOnSelect: true ) );

				list.Add( new MenuItem( "Changer le code", () =>
				{
					StorageCodePanel.Open( storage, StorageCodePanel.Mode.Define );
				}, CloseMenuOnSelect: true ) );

				list.Add( new MenuItem( "Ouvrir le coffre", () =>
				{
					// TODO: brancher TransferInventoryUI.Show( storage.InventoryContainer )
					return Task.CompletedTask;
				}, CloseMenuOnSelect: true ) );
			}
		}
		else
		{
			// Non-proprietaire
			if ( storage.Owner == null && storage.CanBePurchased )
			{
				list.Add( new MenuItem( "Acheter le coffre", () =>
				{
					StorageComponent.Buy( storage );
				}, CloseMenuOnSelect: true ) );
			}
			else if ( storage.IsLocked && storage.HasCode )
			{
				list.Add( new MenuItem( "Entrer le code", () =>
				{
					StorageCodePanel.Open( storage, StorageCodePanel.Mode.Unlock );
				}, CloseMenuOnSelect: true ) );
			}
			else if ( !storage.IsLocked )
			{
				list.Add( new MenuItem( "Ouvrir le coffre", () =>
				{
					// TODO: brancher TransferInventoryUI.Show( storage.InventoryContainer )
					return Task.CompletedTask;
				}, CloseMenuOnSelect: true ) );
			}
		}

		return list;
	}
}
