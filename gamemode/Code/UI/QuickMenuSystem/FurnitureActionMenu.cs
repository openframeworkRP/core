using OpenFramework.Command;
using OpenFramework.Inventory;
using OpenFramework.Systems.Tools;

namespace OpenFramework.UI.QuickMenuSystem;

public record FurnitureActionMenu( GameObject FurnitureObject ) : IQuickMenuInterface
{
	private InventoryItem Item => FurnitureObject?.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndChildren );
	private FurnitureVisual FurnitureVisual => FurnitureObject?.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndChildren );

	public string Title => Item?.Metadata?.Name ?? "Meuble";
	public string SubTitle
	{
		get
		{
			var fv = FurnitureVisual;
			if ( fv == null ) return "";
			if ( fv.OwnerLocked && !fv.IsOwnedBy( Client.Local ) ) return "Verrouille (autre joueur)";
			if ( fv.OwnerLocked ) return fv.IsLocked ? "Fixé · Verrouille" : "Verrouille";
			return fv.IsLocked ? "Fixé" : "";
		}
	}
	public QuickMenuStyle Style => new();

	public List<MenuItem> BuildMenu()
	{
		var list = new List<MenuItem>();

		var fv = FurnitureVisual;
		if ( fv != null )
		{
			bool isOwner = fv.IsOwnedBy( Client.Local );
			bool canManipulate = fv.CanBeManipulatedBy( Client.Local );

			if ( isOwner )
			{
				if ( fv.OwnerLocked )
				{
					list.Add( new MenuItem( "Autoriser autres joueurs", () =>
					{
						Commands.RPC_SetFurnitureOwnerLock( FurnitureObject, false );
					}, CloseMenuOnSelect: true ) );
				}
				else
				{
					list.Add( new MenuItem( "Verrouiller (proprio)", () =>
					{
						Commands.RPC_SetFurnitureOwnerLock( FurnitureObject, true );
					}, CloseMenuOnSelect: true ) );
				}
			}

			if ( !canManipulate )
			{
				// Aucune autre option : non-proprietaire face a un meuble verrouille
			}
			else if ( fv.IsLocked )
			{
				list.Add( new MenuItem( "Défixer", () =>
				{
					Commands.RPC_SetFurnitureLock( FurnitureObject, false );
				}, CloseMenuOnSelect: true ) );
			}
			else
			{
				list.Add( new MenuItem( "Fixer", () =>
				{
					Commands.RPC_SetFurnitureLock( FurnitureObject, true );
				}, CloseMenuOnSelect: true ) );

				list.Add( new MenuItem( "Déplacer", () =>
				{
					var pawn = Client.Local?.PlayerPawn as OpenFramework.Systems.Pawn.PlayerPawn;
					var placer = pawn?.Components.Get<PropPlacer>( FindMode.EnabledInSelfAndDescendants );
					placer?.StartMoving( FurnitureObject );
				}, CloseMenuOnSelect: true ) );

				list.Add( new MenuItem( "Ramasser", () =>
				{
					Commands.RPC_PickupFurniture( FurnitureObject );
				}, CloseMenuOnSelect: true ) );
			}
		}
		else
		{
			list.Add( new MenuItem( "Ramasser", () =>
			{
				Commands.RPC_PickupFurniture( FurnitureObject );
			}, CloseMenuOnSelect: true ) );
		}

		return list;
	}
}
