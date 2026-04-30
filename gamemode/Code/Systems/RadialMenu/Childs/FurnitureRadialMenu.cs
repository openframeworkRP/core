using OpenFramework;
using OpenFramework.Command;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Tools;
using OpenFramework.UI.RadialMenu;

namespace OpenFramework.Systems.RadialMenu;

/// <summary>
/// Menu radial affiché quand un joueur appuie sur F en regardant un meuble posé.
/// </summary>
public class FurnitureRadialMenu : RadialMenuBase
{
	private FurnitureVisual FurnitureVisual => Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndDescendants );

	public override List<RadialMenuItem> BuildItems()
	{
		var list = new List<RadialMenuItem>();
		var fv = FurnitureVisual;

		if ( fv != null )
		{
			bool isOwner = fv.IsOwnedBy( Client.Local );
			bool canManipulate = fv.CanBeManipulatedBy( Client.Local );

			// Bouton verrouillage proprietaire (visible uniquement par le proprietaire)
			if ( isOwner )
			{
				if ( fv.OwnerLocked )
				{
					list.Add( new RadialMenuItem
					{
						Label = "Autoriser autres",
						Icon = "ui/icons/freeze.svg",
						Color = "#5ac864",
						OnSelected = () => Commands.RPC_SetFurnitureOwnerLock( GameObject, false ),
					} );
				}
				else
				{
					list.Add( new RadialMenuItem
					{
						Label = "Verrouiller (proprio)",
						Icon = "ui/icons/freeze.svg",
						Color = "#e2a050",
						OnSelected = () => Commands.RPC_SetFurnitureOwnerLock( GameObject, true ),
					} );
				}
			}

			if ( !canManipulate )
			{
				// Non-proprietaire face a un meuble verrouille : aucune action
				list.Add( new RadialMenuItem
				{
					Label = "Verrouille (autre joueur)",
					Icon = "ui/icons/freeze.svg",
					Color = "#888888",
					OnSelected = () => { },
				} );
			}
			else if ( fv.IsLocked )
			{
				list.Add( new RadialMenuItem
				{
					Label = "Défixer",
					Icon = "ui/icons/freeze.svg",
					Color = "#5ac864",
					OnSelected = () => Commands.RPC_SetFurnitureLock( GameObject, false ),
				} );
			}
			else
			{
				list.Add( new RadialMenuItem
				{
					Label = "Fixer",
					Icon = "ui/icons/freeze.svg",
					Color = "#5b9bd5",
					OnSelected = () => Commands.RPC_SetFurnitureLock( GameObject, true ),
				} );

				list.Add( new RadialMenuItem
				{
					Label = "Déplacer",
					Icon = "ui/icons/transfer.svg",
					Color = "#fdea60",
					OnSelected = () =>
					{
						var pawn = Client.Local?.PlayerPawn as PlayerPawn;
						var placer = pawn?.Components.Get<PropPlacer>( FindMode.EnabledInSelfAndDescendants );
						placer?.StartMoving( GameObject );
					},
				} );

				list.Add( new RadialMenuItem
				{
					Label = "Ramasser",
					Icon = "ui/icons/backpack.svg",
					Color = "#e25050",
					OnSelected = () => Commands.RPC_PickupFurniture( GameObject ),
				} );
			}
		}
		else
		{
			list.Add( new RadialMenuItem
			{
				Label = "Ramasser",
				Icon = "ui/icons/backpack.svg",
				Color = "#e25050",
				OnSelected = () => Commands.RPC_PickupFurniture( GameObject ),
			} );
		}

		list.Add( new RadialMenuItem
		{
			Label = "Annuler",
			Icon = "ui/icons/close.svg",
			Color = "#888888",
			OnSelected = () => { },
		} );

		return list;
	}
}
