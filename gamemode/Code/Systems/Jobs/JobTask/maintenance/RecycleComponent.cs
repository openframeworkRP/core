using Sandbox;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Composant attaché au NPC recycleur.
/// Accepte une liste d'items recyclables et les rachète au joueur.
/// Le prix de revente est celui défini dans l'ItemMetadata (Price).
/// </summary>
public sealed class RecycleComponent : Component
{
	/// <summary>
	/// Liste des items que ce NPC accepte de racheter.
	/// </summary>
	[Property] public List<ItemMetadata> AcceptedItems { get; set; } = new();

	InventoryContainer Container => Client.Local?.PlayerPawn?.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

	/// <summary>
	/// Retourne la quantité d'un item dans l'inventaire du joueur.
	/// </summary>
	public int GetPlayerStock( ItemMetadata item )
	{
		if ( Container == null || item == null ) return 0;

		return Container.Items
			.Where( i => i.Metadata == item )
			.Sum( i => i.Quantity );
	}

	/// <summary>
	/// Retourne le total de tous les items recyclables dans l'inventaire.
	/// </summary>
	public int GetTotalStock()
	{
		if ( Container == null ) return 0;

		return AcceptedItems.Sum( item => GetPlayerStock( item ) );
	}

	/// <summary>
	/// Vend une quantité d'un item spécifique.
	/// Toute la logique (validation job, retrait des items, ajout d'argent) s'exécute
	/// atomiquement côté host : sans ça, le client n'a pas l'autorité pour détruire
	/// l'InventoryItem (network-spawned), seul l'argent était crédité → duplication.
	/// </summary>
	[Rpc.Host]
	public void SellItem( ItemMetadata item, int quantity )
	{
		if ( !Networking.IsHost ) return;
		if ( item == null || quantity <= 0 ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var job = caller.Data?.Job;
		if ( !string.Equals( job, JobList.Maintenance.ToString(), StringComparison.OrdinalIgnoreCase ) )
		{
			caller.Notify( NotificationType.Error, "Vous devez être éboueur pour recycler des déchets." );
			return;
		}

		// Anti-cheat : un client ne peut vendre que les items déclarés sur ce NPC.
		if ( !AcceptedItems.Contains( item ) ) return;

		var container = caller.PlayerPawn?.InventoryContainer;
		if ( container == null ) return;

		int stock = container.Items.Where( i => i.Metadata == item ).Sum( i => i.Quantity );
		if ( stock <= 0 )
		{
			caller.Notify( NotificationType.Error, $"Vous n'avez pas de {item.Name}." );
			return;
		}

		quantity = Math.Min( quantity, stock );
		int total = quantity * item.Price;

		// 1) Retire les items D'ABORD (autorité host) — anti-duplication strict.
		InventoryContainer.Remove( container, item.ResourceName, quantity );

		// 2) Puis crédite l'argent. Client passé explicitement (Rpc.Caller fiable mais
		//    Add(client, amount) est non-ambigu en chaîne d'Rpc.Host).
		MoneySystem.Add( caller, total );

		caller.Notify( NotificationType.Success,
			$"Vous avez vendu {quantity}x {item.Name} pour ${total}." );
	}

	/// <summary>
	/// Vend tous les items recyclables d'un coup. Atomique côté host (anti-duplication).
	/// </summary>
	[Rpc.Host]
	public void SellAll()
	{
		if ( !Networking.IsHost ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var job = caller.Data?.Job;
		if ( !string.Equals( job, JobList.Maintenance.ToString(), StringComparison.OrdinalIgnoreCase ) )
		{
			caller.Notify( NotificationType.Error, "Vous devez être éboueur pour recycler des déchets." );
			return;
		}

		var container = caller.PlayerPawn?.InventoryContainer;
		if ( container == null ) return;

		int totalMoney = 0;
		int totalItems = 0;

		foreach ( var meta in AcceptedItems )
		{
			if ( meta == null ) continue;

			int qty = container.Items.Where( i => i.Metadata == meta ).Sum( i => i.Quantity );
			if ( qty <= 0 ) continue;

			// Retrait atomique côté host avant comptabilisation de l'argent.
			InventoryContainer.Remove( container, meta.ResourceName, qty );
			totalMoney += qty * meta.Price;
			totalItems += qty;
		}

		if ( totalItems <= 0 )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez rien à recycler." );
			return;
		}

		MoneySystem.Add( caller, totalMoney );
		caller.Notify( NotificationType.Success,
			$"Vous avez vendu {totalItems} déchets pour ${totalMoney}." );
	}
}
