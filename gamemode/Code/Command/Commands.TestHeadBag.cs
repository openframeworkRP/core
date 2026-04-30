using Sandbox.Diagnostics;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	/// <summary>
	/// Commande admin : donne un "Sac de tête" dans l'inventaire de l'appelant.
	/// Permet ensuite de tester le menu d'interaction (Mettre / Retirer le sac sur autrui).
	/// </summary>
	[Command( "Donner un sac de tête", ["giveheadbag"], "Donne un sac de tête dans l'inventaire", "ui/icons/item.svg", CommandPermission.Admin )]
	public static void GiveHeadBag()
	{
		RPC_GiveHeadBag();
	}

	[Rpc.Host]
	public static void RPC_GiveHeadBag()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Accès refusé !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Pas de pawn actif." );
			return;
		}

		var container = pawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		if ( container == null )
		{
			caller.Notify( NotificationType.Error, "Pas d'inventaire." );
			return;
		}

		InventoryContainer.Add( container, "head-bag", 1 );
		caller.Notify( NotificationType.Success, "Sac de tête ajouté à ton inventaire." );
		Log.Info( "[TestHeadBag] Sac de tête donné à " + caller.DisplayName );
	}

	/// <summary>
	/// Commande admin : équipe directement un sac de tête sur l'appelant (overlay test).
	/// Utile pour vérifier l'effet visuel sans passer par l'UI inventaire.
	/// </summary>
	[Command( "Équiper un sac de tête", ["equipheadbag"], "Équipe directement un sac de tête sur soi", "ui/icons/item.svg", CommandPermission.Admin )]
	public static void EquipHeadBag()
	{
		RPC_EquipHeadBag();
	}

	[Rpc.Host]
	public static void RPC_EquipHeadBag()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Accès refusé !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Pas de pawn actif." );
			return;
		}

		var equipment = pawn.GameObject.Components
			.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		if ( equipment == null )
		{
			caller.Notify( NotificationType.Error, "Pas de ClothingEquipment." );
			return;
		}

		equipment.EquipFromResourceName( "head-bag" );
		caller.Notify( NotificationType.Success, "Sac de tête équipé. L'overlay doit être actif." );
		Log.Info( "[TestHeadBag] Sac de tête équipé sur " + caller.DisplayName );
	}

	/// <summary>
	/// Commande admin : retire le sac de tête équipé sur l'appelant.
	/// Le sac est détruit (pas transféré dans un inventaire).
	/// </summary>
	[Command( "Retirer le sac de tête", ["removeheadbag"], "Retire le sac de tête équipé sur soi", "ui/icons/item.svg", CommandPermission.Admin )]
	public static void RemoveHeadBag()
	{
		RPC_RemoveHeadBag();
	}

	[Rpc.Host]
	public static void RPC_RemoveHeadBag()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Accès refusé !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Pas de pawn actif." );
			return;
		}

		var equipment = pawn.GameObject.Components
			.Get<ClothingEquipment>( FindMode.EnabledInSelfAndChildren );
		if ( equipment == null )
		{
			caller.Notify( NotificationType.Error, "Pas de ClothingEquipment." );
			return;
		}

		var headItem = equipment.GetEquipped( ClothingEquipment.Slot.Head );
		if ( headItem?.Metadata?.ResourceName != "head-bag" )
		{
			caller.Notify( NotificationType.Warning, "Aucun sac de tête équipé." );
			return;
		}

		headItem.GameObject.Destroy();
		equipment.Container?.MarkDirty();
		caller.Notify( NotificationType.Success, "Sac de tête retiré." );
		Log.Info( "[TestHeadBag] Sac de tête retiré de " + caller.DisplayName );
	}
}
