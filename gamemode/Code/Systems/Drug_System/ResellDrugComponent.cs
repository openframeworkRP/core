using Sandbox;
using OpenFramework.Command;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using static Facepunch.NotificationSystem;

namespace OpenFramework;

public sealed class ResellDrugComponent : Component
{
	[Property] public ItemMetadata Drug { get; set; }
	[Property] public int ResellPrice { get; set; } = 100;
	InventoryContainer Container => Client.Local?.PlayerPawn?.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
	public bool IsDrug( ItemMetadata item )
		=> item.Attributes.ContainsKey( "drug" );

	public int GetPlayerStock()
	{
		if ( Container == null ) return 0;

		return Container.Items
			.Where( i => i.Metadata == Drug )
			.Sum( i => i.Quantity );
	}

	public void Resell( int quantity )
	{
		if ( Drug == null ) { Log.Error( "Drug is null" ); return; }
		if ( quantity <= 0 ) return;

		var stock = GetPlayerStock();
		if ( stock <= 0 )
		{
			Client.Local.Notify( NotificationType.Error, $"Vous n'avez pas de {Drug.Name}." );
			return;
		}

		quantity = Math.Min( quantity, stock );
		var total = quantity * ResellPrice;

		// Récupère l'InventoryItem correspondant à la drug
		var item = Container?.Items
			.FirstOrDefault( i => i.Metadata == Drug );

		if ( item == null ) { Log.Error( "Item not found in inventory" ); return; }

		// Retire du stock sans spawner d'objet physique
		if ( item.Quantity > quantity )
			item.Quantity -= quantity;
		else
			item.GameObject.Destroy();

		MoneySystem.Add( total );

		Client.Local.Notify( NotificationType.Success,
			$"Vous avez vendu {quantity}x {Drug.Name} pour ${total}." );
	}

}
