using Facepunch;
using OpenFramework.Inventory;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Composant attaché aux déchets spawnés dans la ville.
/// Seul un agent d'entretien peut les ramasser via IUse.
/// Donne un item "déchet" dans l'inventaire du joueur.
/// </summary>
public sealed class MaintenanceTrashPickup : Component, IUse
{
	/// <summary>
	/// Le ResourceName de l'ItemMetadata à donner au joueur.
	/// </summary>
	[Property] public ItemMetadata TrashItem { get; set; }

	/// <summary>
	/// Quantité d'items donnés par ramassage.
	/// </summary>
	[Property] public int Quantity { get; set; } = 1;

	public UseResult CanUse( PlayerPawn player )
	{
		if ( player.Client.Data.Job != "maintenance" )
			return "Réservé aux agents d'entretien.";

		return true;
	}

	public void OnUse( PlayerPawn player )
	{
		if ( !Networking.IsHost ) return;

		if ( player.Client.Data.Job != "maintenance" )
		{
			player.Client.Notify( NotificationType.Warning, "Vous devez être agent d'entretien." );
			return;
		}

		if ( TrashItem == null )
		{
			player.Client.Notify( NotificationType.Error, "Aucun item configuré pour ce déchet." );
			return;
		}

		var container = player.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container == null ) return;

		InventoryContainer.Add( container, TrashItem.ResourceName, Quantity );
		player.Client.Notify( NotificationType.Success, $"Vous avez ramassé {Quantity}x {TrashItem.Name}." );

		// Retirer de la liste du manager
		var manager = Scene.GetComponentInChildren<MaintenanceTrashManager>();
		manager?.SpawnedTrash.Remove( GameObject );

		GameObject.Destroy();
	}
}
