using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems.AntiCheat;
using System.Linq;

namespace OpenFramework.Systems;

public static class MoneySystem
{
	public const string ResourceName = "money";

	// ─────────────────────────────────────────────
	//  LECTURE  (pas de RPC, appelable partout)
	// ─────────────────────────────────────────────

	public static int Get()
		=> FindMoneyItem( Client.Local )?.Quantity ?? 0;

	public static int Get( Client client )
		=> FindMoneyItem( client )?.Quantity ?? 0;

	public static bool CanAfford( int amount )
		=> Get() >= amount;

	public static bool CanAfford( Client client, int amount )
		=> Get( client ) >= amount;

	// ─────────────────────────────────────────────
	//  MODIFICATION  (RPC void uniquement)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void Add( int amount )
	{
		if ( amount <= 0 ) return;

		// Rpc.Caller = client appelant (côté serveur dédié, Client.Local est null).
		var client = Rpc.Caller.GetClient();
		if ( client == null ) return;

		InternalAdd( client, amount );
	}

	[Rpc.Host]
	public static void Add( Client client, int amount )
	{
		if ( !Networking.IsHost || client == null || amount <= 0 ) return;
		
		InternalAdd( client, amount );
	}

	private static void InternalAdd( Client client, int amount )
	{
		var inventory = GetInventory( client );
		if ( inventory == null ) return;

		InventoryContainer.Add( inventory, ResourceName, amount );
		Log.Info( $"[Money] +{amount}$ → {client.DisplayName} (total: {Get( client )}$)" );
		AntiCheatLogger.OnMoneyAdd( client, amount );
	}

	/// <summary>
	/// Retire du cash au client appelant. Vérification de solde faite côté serveur (anti-cheat).
	/// </summary>
	[Rpc.Host]
	public static void Remove(int amount )
	{
		if ( amount <= 0 ) return;

		// Rpc.Caller = client appelant (côté serveur dédié, Client.Local est null).
		var client = Rpc.Caller.GetClient();
		if ( client == null ) return;

		// Vérif solde côté serveur : un client ne doit pas pouvoir dépenser plus qu'il n'a.
		if ( !CanAfford( client, amount ) )
		{
			Log.Warning( $"[Money] {client.DisplayName} tente Remove({amount}$) mais n'a que {Get( client )}$" );
			return;
		}

		InternalRemove( client, amount );
	}

	[Rpc.Host]
	public static void Remove( Client client, int amount )
	{
		if ( !Networking.IsHost || client == null || amount <= 0 ) return;

		InternalRemove( client, amount );
	}

	private static void InternalRemove( Client client, int amount )
	{
		var item = FindMoneyItem( client );
		if ( item == null || item.Quantity < amount )
		{
			Log.Warning( $"[Money] {client.DisplayName} n'a pas assez de cash ({Get( client )}$ < {amount}$)" );
			return;
		}

		item.Quantity -= amount;
		Log.Info( $"[Money] -{amount}$ ← {client.DisplayName} (restant: {Get( client )}$)" );
		AntiCheatLogger.OnMoneyRemove( client, amount );
	}

	/// <summary>
	/// Transfert entre deux joueurs.
	/// Vérifie le solde avec CanAfford() avant d'appeler.
	/// </summary>
	[Rpc.Host]
	public static void Transfer( Client from, Client to, int amount )
	{
		if ( !Networking.IsHost || from == null || to == null || amount <= 0 ) return;

		if ( !CanAfford( from, amount ) )
		{
			Log.Warning( $"[Money] Transfer échoué : {from.DisplayName} n'a pas {amount}$" );
			return;
		}

		Remove( from, amount );
		Add( to, amount );
	}

	// ─────────────────────────────────────────────
	//  HELPERS PRIVÉS
	// ─────────────────────────────────────────────

	private static InventoryContainer GetInventory( Client client )
	{
		if ( client == null || client.PlayerPawn == null ) return null;
		// Passe par le getter du PlayerPawn (qui exclut le ClothingContainer)
		return client.PlayerPawn.InventoryContainer;
	}

	private static InventoryItem FindMoneyItem( Client client )
		=> GetInventory( client )?.Items
			?.FirstOrDefault( x => x.Metadata?.ResourceName == ResourceName );
}
