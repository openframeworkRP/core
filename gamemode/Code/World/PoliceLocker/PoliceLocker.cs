using Facepunch;
using OpenFramework.Inventory;
using OpenFramework.Systems.Jobs;
using OpenFramework.Systems.Pawn;
using OpenFramework.UI.World;
using static Facepunch.NotificationSystem;

namespace OpenFramework.World;

[Title( "Police Locker" ), Icon( "local_police" ), Group( "World" )]
public sealed class PoliceLocker : Component, IUse
{
	[Property, Category( "Config" )]
	public List<AvailableItem> AvailableItems { get; set; } = new();

	// ─────────────────────────────────────────────
	//  IUse
	// ─────────────────────────────────────────────

	public UseResult CanUse( PlayerPawn player )
	{
		var job = player.Client?.Data?.Job?.ToLower();
		if ( job != "police" )
			return "Réservé à la police.";
		return true;
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();
	}


	public void OnUse( PlayerPawn player )
	{
		var job = player.Client?.Data?.Job?.ToLower();
		using ( Rpc.FilterInclude( player.Client.Connection ) )
			PoliceLockerUI.Open( this );
	}

	// ─────────────────────────────────────────────
	//  RPC host : donner un item
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void RequestTakeItem( PoliceLocker locker, string itemResource, PlayerPawn player, int quantity = 1)
	{
		if ( !Networking.IsHost ) return;
		if ( !locker.IsValid() || !player.IsValid() ) return;

		var existentionItem = player.InventoryContainer.Items.FirstOrDefault( t => t.Metadata.ResourceName == itemResource );
		
		if ( existentionItem != null )
		{
			if ( existentionItem.Metadata.Attributes.ContainsKey( "service_weapon" ) )
			{
				return;
			}
		}
		// Validation job côté host (anti-triche)
		var job = player.Client?.Data?.Job?.ToLower();

		// Vérification que l'item est bien dans la liste du locker (comparaison par ResourceName)
		var meta = locker.AvailableItems.FirstOrDefault( x => x.Item1 != null && x.Item1.ResourceName == itemResource );
		if ( !meta.Item2.Contains( job ) )
		{
			player.Client?.Notify( NotificationType.Error, "Accès non autorisé." );
			return;
		}
		
		if ( meta.Item1 == null )
		{
			player.Client?.Notify( NotificationType.Error, "Équipement invalide." );
			return;
		}

		var container = player.InventoryContainer;
		if ( container == null )
		{
			player.Client?.Notify( NotificationType.Error, "Inventaire introuvable." );
			return;
		}

		if ( !container.CanAdd( meta.Item1, quantity ) )
		{
			player.Client?.Notify( NotificationType.Warning, "Inventaire plein ou trop lourd." );
			return;
		}

		InventoryContainer.Add( container, itemResource, 1, new Dictionary<string, string>()
		{
			{"service_weapon", "NUMERO-DE-SERIE"},
		});
		InventoryContainer.Add( container, $"mag_" + itemResource, 1,  new Dictionary<string, string>()
		{
			{"service_weapon", "NUMERO-DE-SERIE"},
		});
		InventoryContainer.Add( container, meta.Item1.AmmoType.ResourceName, 60, new Dictionary<string, string>()
		{
			{"service_weapon", "NUMERO-DE-SERIE"},
		});
		player.Client?.Notify( NotificationType.Success, $"{meta.Item1.Name} récupéré." );
	}
}

public class AvailableItem : Component
{
	[Property]
	public ItemMetadata Item1 { get; set; }
	[Property]
	public List<string> Item2 { get; set; }
};
