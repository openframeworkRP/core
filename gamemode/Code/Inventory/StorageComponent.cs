using Sandbox.Diagnostics;
using OpenFramework.Extension;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Inventory;

public class StorageComponent : Component
{
	[Property, Sync( SyncFlags.FromHost ), FeatureEnabled( "Purchase" )]
	public Client Owner { get; set; }

	[Property, FeatureEnabled( "Purchase" )] public bool CanBePurchased { get; set; } = true;
	[Property, Sync( SyncFlags.FromHost ), FeatureEnabled( "Purchase" )] public int Price { get; set; } = 500;

	/// <summary>
	/// Si true : ce storage est un coffre verrouillable par code PIN. La pose
	/// demande un code, et tant qu'il n'est pas deverrouille, ouvrir / ramasser
	/// / deplacer / defixer sont refuses. Si false (defaut) : storage classique
	/// (cabinet, tiroir, casier non protege), accessible librement.
	/// </summary>
	[Property] public bool RequiresCode { get; set; } = false;

	// Verrou : coffre fermé tant qu'IsLocked = true. A la pose, on part
	// verrouillé sans code ; l'owner doit definir son code via SetLockCode.
	[Property, Sync( SyncFlags.FromHost )] public bool IsLocked { get; set; } = true;

	// Indique aux clients qu'un code a ete defini. Le code lui-meme ne fuit
	// jamais au reseau : il est stocke uniquement cote host.
	[Sync( SyncFlags.FromHost )] public bool HasCode { get; set; } = false;

	// Stocke uniquement cote host. Pas de [Sync] : les clients n'en voient
	// jamais la valeur. Le client envoie sa tentative via TryUnlock et le
	// host compare ici.
	private string _lockCode;

	public InventoryContainer InventoryContainer => GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

	[Rpc.Host]
	public static void Buy( StorageComponent storage )
	{
		var caller = Rpc.Caller.GetClient();

		if ( !storage.CanBePurchased )
		{
			caller.Notify( NotificationType.Error, "Ce stockage ne peut pas etre achetee" );
			return;
		}

		if ( storage.Owner != null ) return;

		if ( caller.Data.Bank <= storage.Price )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas assez d'argent" );
			return;
		}

		caller.Data.Bank -= storage.Price;
		storage.Owner = caller;
		caller.Notify( NotificationType.Success, "Vous venez d'acheter le casier" );
	}

	/// <summary>
	/// Definit l'owner d'un coffre fraichement pose. Appele par PropPlacer
	/// apres le spawn pour que le placeur devienne proprietaire sans payer
	/// (il a deja achete l'item depuis son inventaire).
	/// </summary>
	public void AssignOwnerFromHost( Client owner )
	{
		Assert.True( Networking.IsHost );
		Owner = owner;
		IsLocked = true;
		HasCode = false;
		_lockCode = null;
	}

	/// <summary>
	/// L'owner definit (ou change) le code a 4 chiffres. Apres ca le coffre
	/// reste verrouille : il faut ensuite TryUnlock pour y acceder.
	/// </summary>
	[Rpc.Host]
	public static void SetLockCode( StorageComponent storage, string code )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || storage == null ) return;

		if ( storage.Owner != caller )
		{
			caller.Notify( NotificationType.Error, "Vous n'etes pas proprietaire de ce coffre." );
			return;
		}

		if ( string.IsNullOrEmpty( code ) || code.Length != 4 || !code.All( char.IsDigit ) )
		{
			caller.Notify( NotificationType.Error, "Le code doit contenir exactement 4 chiffres." );
			return;
		}

		storage._lockCode = code;
		storage.HasCode = true;
		storage.IsLocked = true;
		caller.Notify( NotificationType.Success, "Code defini. Le coffre est verrouille." );
	}

	/// <summary>
	/// N'importe quel joueur peut tenter le code. Si correct, deverrouille.
	/// </summary>
	[Rpc.Host]
	public static void TryUnlock( StorageComponent storage, string code )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || storage == null ) return;

		if ( !storage.HasCode )
		{
			caller.Notify( NotificationType.Error, "Ce coffre n'a pas encore de code." );
			return;
		}

		if ( storage._lockCode != code )
		{
			caller.Notify( NotificationType.Error, "Code incorrect." );
			return;
		}

		storage.IsLocked = false;
		caller.Notify( NotificationType.Success, "Coffre deverrouille." );
	}

	/// <summary>
	/// Reverrouille manuellement le coffre (sans changer le code). Owner only.
	/// </summary>
	[Rpc.Host]
	public static void Lock( StorageComponent storage )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || storage == null ) return;

		if ( storage.Owner != caller )
		{
			caller.Notify( NotificationType.Error, "Vous n'etes pas proprietaire de ce coffre." );
			return;
		}

		if ( !storage.HasCode )
		{
			caller.Notify( NotificationType.Error, "Definissez d'abord un code." );
			return;
		}

		storage.IsLocked = true;
		caller.Notify( NotificationType.Info, "Coffre verrouille." );
	}

	/// <summary>
	/// Deverrouille sans code (legacy / admin). A conserver pour d'autres
	/// storages non-verrouilles par code.
	/// </summary>
	[Rpc.Host]
	public static void Unlock( StorageComponent storage )
	{
		var caller = Rpc.Caller.GetClient();
		if ( caller == null || storage == null ) return;

		// Si le coffre a un code, on refuse ce chemin : il faut TryUnlock
		if ( storage.HasCode )
		{
			caller.Notify( NotificationType.Error, "Ce coffre necessite un code." );
			return;
		}

		storage.IsLocked = false;
		caller.Notify( NotificationType.Info, "Coffre deverrouille." );
	}
}
