using Facepunch;
using Sandbox.Diagnostics;
using Sandbox.World;
using OpenFramework.World;
using OpenFramework.Api;
using OpenFramework.ChatSystem;
using OpenFramework.Extension;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Npc;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	/// <summary>
	/// Logue toute commande admin invoquée depuis le client (chat ou menu admin).
	/// Re-vérifie IsAdmin côté serveur pour qu'un client trafiqué ne puisse pas
	/// injecter de faux logs.
	/// </summary>
	[Rpc.Host]
	public static void RPC_LogAdminCommand( string commandName, ulong targetSteamId, string argsJson )
	{
		if ( !Networking.IsHost ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null || !caller.IsAdmin ) return;

		var target = targetSteamId == 0 ? null : targetSteamId.ToString();
		_ = ApiComponent.Instance?.LogAdminAction(
			caller.SteamId.ToString(),
			$"cmd:{commandName}",
			targetSteamId: target,
			reason: argsJson,
			source: "ingame" );
	}

	[Rpc.Host]
	public static void RPC_GiveMoney( Client target, int amount )
	{
		Assert.True( Networking.IsHost );

		if ( !target.IsValid() )
			return;

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		// On desactive cette condition vu que c'est du darkrp
		/*if ( target == caller )
		{
			caller.Notify(NotificationType.Error, "You cannot give to yourself !" );
			return;
		}*/

		MoneySystem.Add( target, amount );
		target.Notify( NotificationType.Success, $"+ {amount}€" );

		// TODO: Dispatch the event to the connected discord server.
		//DiscordEvents.Dispatch( new DGiveMoney(Rpc.Caller, target, amount) );
	}

	[Rpc.Host]
	public static void RPC_GiveBankMoney( Client target, int amount )
	{
		Assert.True( Networking.IsHost );

		if ( !target.IsValid() )
			return;

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		// On desactive cette condition vu que c'est du darkrp
		/*if ( target == caller )
		{
			caller.Notify(NotificationType.Error, "You cannot give to yourself !" );
			return;
		}*/

		target.Data.Bank += amount;
		target.Notify( NotificationType.Success, $"(Banque) + {amount}€." );

		// TODO: Dispatch the event to the connected discord server.
		//DiscordEvents.Dispatch( new DGiveBankMoney(Rpc.Caller, target, amount) );
	}

	[Rpc.Host]
	public static async void RPC_Ban( Client target, int duration, string reason )
	{
		Assert.True( Networking.IsHost );

		if ( !target.IsValid() )
			return;

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var result = await ApiComponent.Instance.BanUser(target.SteamId.ToString(), reason, caller.SteamId.ToString());
		if ( !result.Success )
		{
			caller.Notify( NotificationType.Error ,"Une erreur est survenu dans le stockage du joueur au sein de l'api. " +
			                                       "Cepandant il a bien été kick du serveur." );
		}
		
		// TODO: Add ban duration

		target.Connection.Kick( $"Banned: {reason}\n Please consult our website: https://github.com/openframeworkRP" );

		// TODO: Dispatch the event to the connected discord server.
		//DiscordEvents.Dispatch( new DBanClient(Rpc.Caller, user.Username, duration, reason) );
	}

	[Rpc.Host]
	public static async void RPC_Unban( ulong steamid )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var result = await ApiComponent.Instance.UnBanUser(steamid.ToString(), "command system", caller.SteamId.ToString());
		if ( !result.Success )
		{
			caller.Notify( NotificationType.Error ,"Une erreur est survenu lors de débannissement du joueur.");
		}

		// TODO: Dispatch the event to the connected discord server.
		//DiscordEvents.Dispatch( new DUnbanClient(Rpc.Caller, user.Username) );
	}

	[Rpc.Host]
	public static void RPC_Noclip( Client target )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Accès refusé !" );
			return;
		}

		if ( target?.PlayerPawn == null ) return;

		// On change juste la variable synchronisée
		target.PlayerPawn.IsNoclipping = !target.PlayerPawn.IsNoclipping;

		// Notification à la cible
		string status = target.PlayerPawn.IsNoclipping ? "activé" : "désactivé";
		target.Notify( NotificationType.Success, $"Noclip {status}." );
	}

	[Rpc.Host]
	public static void RPC_TeleportTo( Client target )
	{
		Assert.True( Networking.IsHost );
		var caller = Rpc.Caller.GetClient();

		if ( !caller.IsAdmin ) return;
		if ( target?.PlayerPawn == null || caller.PlayerPawn == null ) return;

		var callerPawn = caller.PlayerPawn;
		var targetPawn = target.PlayerPawn;

		// Calcul de la position cible (devant la cible)
		Vector3 targetPos = targetPawn.WorldPosition + (targetPawn.WorldRotation.Forward * 60f) + (Vector3.Up * 5f);

		// On utilise la même méthode Broadcast pour que TON client soit aussi au courant
		callerPawn.ForceTeleport( targetPos );

		caller.Notify( NotificationType.Success, $"Téléporté vers {target.DisplayName}." );
	}

	[Rpc.Host]
	public static void RPC_Bring( Client target )
	{
		Assert.True( Networking.IsHost );
		var caller = Rpc.Caller.GetClient();

		if ( !caller.IsAdmin || target?.PlayerPawn == null ) return;

		Vector3 targetPos = caller.PlayerPawn.WorldPosition + (caller.PlayerPawn.WorldRotation.Forward * 70f);

		caller.Notify( NotificationType.Success, $"Téléporté vers {target.DisplayName}." );
		// On utilise une méthode Broadcast ou on appelle directement sur le pawn
		// pour que le CLIENT de la cible sache qu'il DOIT se téléporter.
		target.PlayerPawn.ForceTeleport( targetPos );
	}

	[Rpc.Host]
	public static void RPC_Kick( Client target, string reason = "" )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		if ( target.Connection.IsHost )
		{
			caller.Notify( NotificationType.Error, "You cannot kick the host." );
			return;
		}

		if(target == caller)
		{
			caller.Notify( NotificationType.Error, "You cannot kick yourself." );
			return;
		}

		ChatUI.Receive(new ChatUI.ChatMessage() {  HostMessage = true, Message = $"{target.DisplayName} has been kicked from the server.({reason})" } );
		target.Connection.Kick( reason );
	}

	[Rpc.Host]
	public static void RPC_GiveItem( Client target, string itemId, int quantity = 1 )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();

		// Vérif permission
		/*if ( !caller.IsAdmin || !caller.Connection.IsHost )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès à cette commande !" );
			return;
		}*/

		// Vérif validité cible
		if ( !target.IsValid() || target.PlayerPawn == null )
		{
			caller.Notify( NotificationType.Warning, "La cible est invalide ou n'a pas de Pawn actif." );
			return;
		}

		// Récupération du metadata
		var metadata = ItemMetadata.All.FirstOrDefault( x =>
			x.ResourceName.Equals( itemId, StringComparison.OrdinalIgnoreCase ) );

		if ( metadata == null )
		{
			caller.Notify( NotificationType.Warning, $"L'item '{itemId}' n'existe pas." );
			return;
		}

		// Récupération du container du joueur
		var container = target.PlayerPawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		if ( container == null || !container.IsValid() )
		{
			caller.Notify( NotificationType.Warning, $"{target.DisplayName} n'a pas d'inventaire valide." );
			return;
		}

		// Appel logique principale — côté host
		InventoryContainer.Add( container, itemId, quantity );

		// Notifications
		if ( target != caller )
		{
			target.Notify( NotificationType.Success,
				$"+{quantity} <font color='green'>{metadata.Name}</font>" );
		}

		caller.Notify( NotificationType.Success, $"+{quantity} <font color='green'>{metadata.Name}</font> {target.DisplayName}" );
	}

	/// <summary>
	/// Achat d'un item chez un PNJ vendor. Vérifie côté host : solde suffisant ET place
	/// dans l'inventaire. Si l'une des deux conditions échoue, la transaction est annulée
	/// et le joueur est notifié — aucun argent ni item n'est modifié.
	/// </summary>
	[Rpc.Host]
	public static void RPC_BuyItem( string itemId )
	{
		Assert.True( Networking.IsHost );

		var client = Rpc.Caller.GetClient();
		if ( client == null || client.Data == null || client.PlayerPawn == null ) return;

		var meta = ItemMetadata.All.FirstOrDefault( x =>
			x.ResourceName.Equals( itemId, StringComparison.OrdinalIgnoreCase ) );

		if ( meta == null )
		{
			client.Notify( NotificationType.Error, "Item introuvable." );
			return;
		}

		// Vérification solde
		if ( client.Data.Bank < meta.Price )
		{
			client.Notify( NotificationType.Error, $"Fonds insuffisants. (Requis : {meta.Price}$, disponible : {client.Data.Bank}$)" );
			return;
		}

		// Vérification inventaire
		var container = client.PlayerPawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		if ( container == null || !container.IsValid() )
		{
			client.Notify( NotificationType.Error, "Inventaire invalide." );
			return;
		}

		if ( !container.CanAdd( meta, 1 ) )
		{
			client.Notify( NotificationType.Error, "Votre inventaire est plein." );
			return;
		}

		// Transaction atomique : argent d'abord, puis item
		client.Data.Bank -= meta.Price;
		InventoryContainer.Add( container, itemId, 1 );

		client.Notify( NotificationType.Success, $"+{meta.Name}({meta.Price}$)" );
	}

	[Rpc.Host]
	public static void RPC_PickupFurniture( GameObject furnitureObj )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) { Log.Warning( "[RPC_PickupFurniture] furnitureObj invalide" ); return; }

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) { Log.Warning( "[RPC_PickupFurniture] caller null" ); return; }

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() ) { Log.Warning( "[RPC_PickupFurniture] pawn invalide" ); return; }

		// Verrou proprietaire : un autre joueur ne peut pas ramasser un meuble
		// tant que le proprietaire ne l'a pas deverrouille.
		var fvOwner = furnitureObj.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndChildren );
		if ( fvOwner != null && !fvOwner.CanBeManipulatedBy( caller ) )
		{
			caller.Notify( NotificationType.Error, "Cet objet appartient a un autre joueur." );
			return;
		}

		// Un coffre a code verrouille ne peut pas etre ramasse
		var storageLocked = furnitureObj.Components.Get<StorageComponent>( FindMode.EverythingInSelfAndChildren );
		if ( storageLocked != null && storageLocked.RequiresCode && storageLocked.IsLocked )
		{
			caller.Notify( NotificationType.Error, "Coffre verrouille : deverrouillez-le d'abord." );
			return;
		}

		var item = furnitureObj.Components.Get<InventoryItem>( FindMode.EverythingInSelfAndChildren );
		Log.Info( $"[RPC_PickupFurniture] objet={furnitureObj.Name} InventoryItem={( item != null ? item.Metadata?.ResourceName ?? "metadata null" : "ABSENT" )}" );
		if ( item?.Metadata == null )
		{
			caller.Notify( NotificationType.Error, "Impossible de ramasser cet objet." );
			return;
		}

		var container = pawn.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container == null )
		{
			caller.Notify( NotificationType.Error, "Pas d'inventaire disponible." );
			return;
		}

		var resourceName = item.Metadata.ResourceName;

		InventoryContainer.Add( container, resourceName, 1 );

		if ( caller.Data != null )
			caller.Data.CurrentProps = Math.Max( 0, caller.Data.CurrentProps - 1 );

		caller.Notify( NotificationType.Success, $"{item.Metadata.Name} ramassé." );

		furnitureObj.Destroy();
	}

	[Rpc.Host]
	public static void RPC_SetFurnitureLock( GameObject furnitureObj, bool locked )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		// Un coffre a code verrouille ne peut pas etre defige (ce qui
		// permettrait de le pousser ou de le deplacer). Il faut d'abord
		// entrer son code. Les storages classiques sans code ne sont
		// pas concernes.
		var storageFreeze = furnitureObj.Components.Get<StorageComponent>( FindMode.EverythingInSelfAndChildren );
		if ( storageFreeze != null && storageFreeze.RequiresCode && storageFreeze.IsLocked && !locked )
		{
			caller.Notify( NotificationType.Error, "Coffre verrouille : deverrouillez-le d'abord." );
			return;
		}

		var fv = furnitureObj.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndChildren );
		if ( fv == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet ne peut pas être fixé." );
			return;
		}

		// Verrou proprietaire : un autre joueur ne peut pas (de)fixer un meuble
		// tant que le proprietaire ne l'a pas deverrouille.
		if ( !fv.CanBeManipulatedBy( caller ) )
		{
			caller.Notify( NotificationType.Error, "Cet objet appartient a un autre joueur." );
			return;
		}

		fv.UpdateFreeze( locked );
		caller.Notify( NotificationType.Success, locked ? "Objet fixé." : "Objet défixé." );
	}

	/// <summary>
	/// Bascule le verrou proprietaire d'un meuble. Seul le placeur (PlacedBySteamId)
	/// est autorise. Quand OwnerLocked=true, les autres joueurs ne peuvent ni
	/// deplacer, ni ramasser, ni fixer, ni interagir avec ce meuble.
	/// </summary>
	[Rpc.Host]
	public static void RPC_SetFurnitureOwnerLock( GameObject furnitureObj, bool locked )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var fv = furnitureObj.Components.Get<FurnitureVisual>( FindMode.EverythingInSelfAndChildren );
		if ( fv == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet n'est pas verrouillable." );
			return;
		}

		// Meuble de map (PlacedBySteamId=0) : pas de proprietaire, action refusee.
		if ( fv.PlacedBySteamId == 0 )
		{
			caller.Notify( NotificationType.Error, "Cet objet n'a pas de proprietaire." );
			return;
		}

		// Seul le proprietaire peut basculer son propre verrou.
		if ( fv.PlacedBySteamId != caller.SteamId )
		{
			caller.Notify( NotificationType.Error, "Vous n'etes pas le proprietaire de cet objet." );
			return;
		}

		fv.OwnerLocked = locked;
		caller.Notify( NotificationType.Success, locked
			? "Verrouille : vous seul pouvez interagir."
			: "Deverrouille : autres joueurs autorises." );
	}

	[Rpc.Host]
	public static void RPC_ToggleLamp( GameObject furnitureObj, bool on )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() ) return;

		// Validation distance : meme limite que les autres interactions furniture
		if ( Vector3.DistanceBetween( pawn.WorldPosition, furnitureObj.WorldPosition ) > 250f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour interagir." );
			return;
		}

		var lamp = furnitureObj.Components.Get<OpenFramework.Systems.Tools.LampComponent>( FindMode.EverythingInSelfAndChildren );
		if ( lamp == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet n'est pas une lampe." );
			return;
		}

		// Note : pas de check OwnerLocked ici. Allumer/eteindre une lampe est
		// une interaction "passive" qui ne deplace ni n'enleve rien — accessible
		// a tous, meme si le meuble appartient a quelqu'un d'autre.

		lamp.SetOn( on );
		Log.Info( $"[RPC_ToggleLamp] {caller.DisplayName} a {(on ? "allumé" : "éteint")} une lampe" );
	}

	[Rpc.Host]
	public static void RPC_ToggleGrill( GameObject furnitureObj, bool on )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() ) return;

		if ( Vector3.DistanceBetween( pawn.WorldPosition, furnitureObj.WorldPosition ) > 250f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour interagir." );
			return;
		}

		var grill = furnitureObj.Components.Get<OpenFramework.Systems.Cooking.GrillStation>( FindMode.EverythingInSelfAndChildren );
		if ( grill == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet n'est pas un grill." );
			return;
		}

		// Note : pas de check OwnerLocked ici. Allumer/eteindre un grill est
		// une interaction "passive" — accessible a tous meme sur meuble verrouille.

		grill.SetLit( on );
		Log.Info( $"[RPC_ToggleGrill] {caller.DisplayName} a {(on ? "allumé" : "éteint")} un grill" );
	}

	[Rpc.Host]
	public static void RPC_ToggleFryer( GameObject furnitureObj, bool on )
	{
		Assert.True( Networking.IsHost );

		if ( furnitureObj == null || !furnitureObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() ) return;

		if ( Vector3.DistanceBetween( pawn.WorldPosition, furnitureObj.WorldPosition ) > 250f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour interagir." );
			return;
		}

		var fryer = furnitureObj.Components.Get<OpenFramework.Systems.Cooking.FryerStation>( FindMode.EverythingInSelfAndChildren );
		if ( fryer == null )
		{
			caller.Notify( NotificationType.Error, "Cet objet n'est pas une friteuse." );
			return;
		}

		fryer.SetLit( on );
		Log.Info( $"[RPC_ToggleFryer] {caller.DisplayName} a {(on ? "allumé" : "éteint")} une friteuse" );
	}

	[Rpc.Host]
	public static void RPC_GiveProps( Client target, string itemId, int quantity = 1 )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();

		// Vérif validité cible
		if ( !target.IsValid() || target.PlayerPawn == null )
		{
			caller.Notify( NotificationType.Warning, "La cible est invalide ou n'a pas de Pawn actif." );
			return;
		}

		// Récupération du metadata
		var metadata = ItemMetadata.All.FirstOrDefault( x =>
			x.ResourceName.Equals( itemId, StringComparison.OrdinalIgnoreCase ) );

		if ( metadata == null )
		{
			caller.Notify( NotificationType.Warning, $"L'item '{itemId}' n'existe pas." );
			return;
		}

		if ( target.Data != null )
		{
			target.Data.CurrentProps += quantity;
		}


		// Récupération du container du joueur
		var container = target.PlayerPawn.GameObject.Components
			.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );

		if ( container == null || !container.IsValid() )
		{
			caller.Notify( NotificationType.Warning, $"{target.DisplayName} n'a pas d'inventaire valide." );
			return;
		}

		// Appel logique principale — côté host
		InventoryContainer.Add( container, itemId, quantity );

	}


	[Rpc.Host]
	public static void RPC_GiveTool( Client target, string tools)
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		/*
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}
		*/
		if ( !target.IsValid() )
			return;

		var metadata = EquipmentResource.All.FirstOrDefault( x => x.ResourceName == tools );
		if ( metadata == null )
		{
			caller.Notify( NotificationType.Error, $"Aucune arme de type {tools} a été trouvée." );
			return;
		}

		target.PlayerPawn.Inventory.Give( metadata );
	}

	[Rpc.Host]
	public static void RPC_RemoveTool( Client target, string tools )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		/*
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}
		*/
		if ( !target.IsValid() )
			return;

		var metadata = EquipmentResource.All.FirstOrDefault( x => x.ResourceName == tools );
		if ( metadata == null )
		{
			caller.Notify( NotificationType.Error, $"Aucune arme de type {tools} a été trouvée." );
			return;
		}

		target.PlayerPawn.Inventory.Remove( metadata );
	}

	[Rpc.Host]
	public static void RPC_GiveWeapon( Client target, string weapon )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		if ( !target.IsValid() )
			return;

		var metadata = EquipmentResource.All.FirstOrDefault( x => string.Equals( x.ResourceName, weapon, StringComparison.OrdinalIgnoreCase ) );
		if ( metadata == null )
		{
			caller.Notify( NotificationType.Error, $"Aucune arme de type {weapon} a été trouvée." );
			return;
		}

		target.PlayerPawn.Inventory.Give( metadata );
		if ( target != caller )
			target.Notify( NotificationType.Generic, $"Un/une {metadata.Name} vous a été give par {caller.DisplayName}." );
		caller.Notify( NotificationType.Success, $"Vous avez give un/une {metadata.Name} à {target.DisplayName}" );
	}

	[Rpc.Host]
	public static void RPC_HarvestWeedPot( GameObject weedPotObj )
	{
		Assert.True( Networking.IsHost );
		if ( weedPotObj == null || !weedPotObj.IsValid() ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() ) return;

		var pot = weedPotObj.Components.Get<WeedPot>( FindMode.EverythingInSelfAndDescendants );
		if ( pot == null )
		{
			caller.Notify( NotificationType.Error, "Pot introuvable." );
			return;
		}

		if ( Vector3.DistanceBetween( pawn.WorldPosition, weedPotObj.WorldPosition ) > 200f )
		{
			caller.Notify( NotificationType.Error, "Trop loin pour récolter." );
			return;
		}

		var useResult = pot.CanUse( pawn );
		if ( !useResult.CanUse )
		{
			caller.Notify( NotificationType.Error, string.IsNullOrEmpty( useResult.Reason )
				? "La plante n'est pas encore prête."
				: useResult.Reason );
			return;
		}

		Log.Info( $"[RPC_HarvestWeedPot] {caller.DisplayName} récolte {weedPotObj.Name}" );
		pot.OnUse( pawn );
		caller.Notify( NotificationType.Success, "Plante récoltée !" );
	}

	[Rpc.Host]
	public static void RPC_Mute( Client target, float delay )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();

		if ( target.IsGlobalVocalMuted )
		{
			caller.Notify( NotificationType.Info, $"{target.DisplayName} est déjà mute." );
			return;
		}

		target.GetComponent<PlayerVoiceComponent>().Enabled = false;
		target.IsGlobalVocalMuted = true;

		if ( delay == -1 )
			target.MuteIndefinite = true;
		else
			target.UntilUnmuteEndTime = Time.Now + delay;

		caller.Notify( NotificationType.Info, $"Vous avez mute {target.DisplayName} {(delay != -1 ? " pendant " + TimeUtils.DelayToReadable( delay ) : "")}." );
		if ( target != caller )
			target.Notify( NotificationType.Info, $"Vous avez été mute par un admin {(delay != -1 ? " pendant " + TimeUtils.DelayToReadable( delay ) : "")}." );
	}

	[Rpc.Host]
	public static void RPC_Unmute( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();

		if ( !target.IsGlobalVocalMuted )
		{
			caller.Notify( NotificationType.Info, $"{target.DisplayName} est déjà unmute." );
			return;
		}

		target.GetComponent<PlayerVoiceComponent>(true).Enabled = true;
		target.IsGlobalVocalMuted = false;
		target.MuteIndefinite = false;
		caller.Notify( NotificationType.Info, $"Vous avez unmute {target.DisplayName}" );

		if ( target != caller )
			target.Notify( NotificationType.Info, $"Vous avez été unmute par un admin." );
	}

	[Rpc.Host]
	public static void RPC_Godmode( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		if ( pawn.HealthComponent.IsGodMode )
		{
			pawn.HealthComponent.IsGodMode = false;
			caller.Notify( NotificationType.Info, $"{target.DisplayName} n'est désormais plus en godmode." );

			if(target != caller)
				target.Notify( NotificationType.Info, $"Vous n'êtes plus en godmode." );
			return;
		}
		else
		{
			pawn.HealthComponent.IsGodMode = true;
			caller.Notify( NotificationType.Info, $"{target.DisplayName} est en désormais en godmode." );
			if ( target != caller )
				target.Notify( NotificationType.Info, $"Vous êtes en godmode." );
		}
	}

	[Rpc.Host]
	public static void RPC_Slap( Client target, float damage )
	{
		Assert.True( Networking.IsHost );

		if ( target == null ) return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid() || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		// 1. Calcul de la direction du coup (Admin -> Cible)
		Vector3 forceDir = (pawn.WorldPosition - caller.PlayerPawn.WorldPosition).Normal;
		// On ajoute une légère inclinaison vers le haut pour le côté "punch"
		Vector3 punchForce = (forceDir * 200f) + (Vector3.Up * 150f);

		// 2. Infliger les dégâts (déclenche déjà les particules de sang via HealthComponent)
		pawn.HealthComponent.TakeDamage( new (
			caller.PlayerPawn,
			damage,
			Hitbox: HitboxTags.Head,
			Force: punchForce,
			Position: pawn.Head.WorldPosition
		) );

		// 3. Propulsion physique immédiate via le CharacterController
		if ( pawn.CharacterController.IsValid() )
		{
			// On utilise Punch pour décoller légèrement du sol et appliquer la vélocité
			pawn.CharacterController.Punch( punchForce );
		}

		// 4. Feedback visuel automatique
		// Ta méthode TakeDamage appelle déjà ProceduralHitReaction sur les AnimationHelpers, 
		// ce qui fera tressaillir le corps du perso sans aucun code supplémentaire !

		caller.Notify( NotificationType.Info, $"Vous avez mis une droite à {target.DisplayName}." );
	}

	[Rpc.Host]
	public static void RPC_Freeze( Client target, float delay )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		if ( pawn.IsFrozen )
		{
			pawn.IsFrozen = false;
			pawn.FreezeIndefinite = false;
			caller.Notify( NotificationType.Info, $"{target.DisplayName} n'est désormais plus freeze." );

			if ( target != caller )
				target.Notify( NotificationType.Info, $"Vous n'êtes plus freeze." );
			return;
		}
		else
		{
			if( delay == -1)
				pawn.FreezeIndefinite = true;
			else
				pawn.TimeUntilUnfreeze = delay;
			pawn.IsFrozen = true;
			caller.Notify( NotificationType.Info, $"{target.DisplayName} est en désormais freeze {(delay != -1 ? " pendant " + TimeUtils.DelayToReadable( delay ) : "")}." );

			if ( target != caller )
				target.Notify( NotificationType.Info, $"Vous êtes freeze {(delay != -1 ? " pendant " + TimeUtils.DelayToReadable( delay ) : "")}." );
		}
	}

	[Rpc.Host]
	public static void RPC_Respawn( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn.HealthComponent.State != LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} est deja en vie." );
			return;
		}

		target.Respawn(true);
	}

	[Rpc.Host]
	public static void RPC_Kill( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		// Godmode bypass : on le retire temporairement pour que Kill fonctionne.
		pawn.HealthComponent.IsGodMode = false;

		pawn.HealthComponent.TakeDamage( new(
			caller.PlayerPawn,
			999999f,
			Hitbox: HitboxTags.Head,
			Position: pawn.WorldPosition
		) );

		caller.Notify( NotificationType.Success, $"Vous avez tué {target.DisplayName}." );
		if ( target != caller )
			target.Notify( NotificationType.Warning, "Vous avez été tué par un administrateur." );
	}

	[Rpc.Host]
	public static void RPC_RespawnHospital( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var spawns = Constants.Instance.HospitalRespawnPositions;

		if( spawns == null || spawns.Count == 0 )
		{
			RPC_RespawnInPlace( target );
			return;
		}

		var spawnRandom = spawns[Game.Random.Int( Constants.Instance.HospitalRespawnPositions.Count - 1 )];

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid() )
		{
			Log.Warning( $"[RPC_RespawnHospital] Pawn invalide pour {target.DisplayName}, annulé." );
			return;
		}

		if ( pawn.HealthComponent.State != LifeState.Dead )
		{
			Log.Warning( $"[RPC_RespawnHospital] {target.DisplayName} n'est pas mort (State={pawn.HealthComponent.State}), annulé." );
			return;
		}

		Log.Info( $"[RPC_RespawnHospital] Respawn hôpital pour {target.DisplayName} à {spawnRandom.WorldPosition}" );
		target.RespawnInHospital( true, spawnRandom.WorldPosition );
	}


	[Rpc.Host]
	public static void RPC_RespawnInPlace( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn.HealthComponent.State != LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} est deja en vie." );
			return;
		}

		target.RespawnInPlaceDeath( true, pawn.LocalPosition );
	}

	[Rpc.Host]
	public static void RPC_RespawnInPrison( Client target )
	{
		Assert.True( Networking.IsHost );

		if ( target == null )
			return;

		var spawns = Constants.Instance.PrisonSpawnPositions;

		if ( spawns == null || spawns.Count == 0 )
		{
			RPC_RespawnInPlace( target );
			return;
		}

		var spawnRandom = spawns[Game.Random.Int( Constants.Instance.PrisonSpawnPositions.Count - 1 )];

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		pawn.WorldPosition = spawnRandom.WorldPosition;

		pawn.OnRespawnInHospital();

		target.RespawnInHospital( true, spawnRandom.WorldPosition );
	}

	[Rpc.Host]
	public static void RPC_SpawnCar()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas de pawn actif." );
			return;
		}

		// Spawn devant le joueur
		var spawnPos = pawn.WorldPosition + pawn.WorldRotation.Forward * 200f;
		var spawnRot = pawn.WorldRotation;

		Spawnable.Server( "prefabs/vehicles/vehicles car49.prefab", spawnPos, spawnRot );

		caller.Notify( NotificationType.Success, "Véhicule spawné devant vous." );
	}

	[Rpc.Host]
	public static void RPC_SpawnTrailer()
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var pawn = caller.PlayerPawn;
		if ( pawn == null || !pawn.IsValid() )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas de pawn actif." );
			return;
		}

		var spawnPos = pawn.WorldPosition + pawn.WorldRotation.Forward * 200f;
		var spawnRot = pawn.WorldRotation;

		Spawnable.Server( "prefabs/vehicles/trailer_base.prefab", spawnPos, spawnRot );

		caller.Notify( NotificationType.Success, "Remorque spawnée devant vous." );
	}

	[Rpc.Host]
	public static void RPC_AddHunger( Client target, float amount )
	{
		Assert.True( Networking.IsHost );

		if ( target == null ) return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid() || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		// Application de la valeur (avec un clamp optionnel pour ne pas dépasser 100)
		target.Data.Hunger = Math.Clamp( target.Data.Hunger + amount, 0, 100 );

		// Notification
		if ( caller == target )
			caller.Notify( NotificationType.Success, $"Vous avez consommé un produit (+{amount} faim)." );
		else
			caller.Notify( NotificationType.Success, $"Vous avez nourri {target.DisplayName}." );
	}

	[Rpc.Host]
	public static void RPC_AddThirst( Client target, float amount )
	{
		Assert.True( Networking.IsHost );

		if ( target == null ) return;

		var caller = Rpc.Caller.GetClient();
		var pawn = target.PlayerPawn;

		if ( pawn == null || !pawn.IsValid() || pawn.HealthComponent.State == LifeState.Dead )
		{
			caller.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}

		// Application de la valeur
		target.Data.Thirst = Math.Clamp( target.Data.Thirst + amount, 0, 100 );

		// Notification
		if ( caller == target )
			caller.Notify( NotificationType.Success, $"Vous vous êtes désaltéré (+{amount} soif)." );
		else
			caller.Notify( NotificationType.Success, $"Vous avez donné à boire à {target.DisplayName}." );
	}

	/// <summary>
	/// Bascule le verrou du serveur. Quand actif, ServerManager.CheckWhitelistAndBan kick
	/// toute nouvelle connexion qui n'est pas dans la liste admin. Les joueurs deja
	/// connectes ne sont pas affectes — la commande agit uniquement sur les futures
	/// connexions. Re-verifie IsAdmin cote host pour empecher un client trafique de
	/// lock/unlock le serveur.
	/// </summary>
	[Rpc.Host]
	public static void RPC_SetServerLock( bool locked )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		if ( ServerManager.IsServerLocked == locked )
		{
			caller.Notify( NotificationType.Info,
				locked ? "Le serveur est déjà verrouillé." : "Le serveur est déjà déverrouillé." );
			return;
		}

		ServerManager.IsServerLocked = locked;
		Log.Info( $"[Lock] {caller.DisplayName} ({caller.SteamId}) a {(locked ? "verrouille" : "deverrouille")} le serveur." );

		// Notification globale pour que tous les admins en jeu voient l'etat actuel.
		ChatUI.Receive( new ChatUI.ChatMessage()
		{
			HostMessage = true,
			Message = locked
				? $"🔒 Le serveur a été verrouillé par {caller.DisplayName} (admins uniquement)."
				: $"🔓 Le serveur a été déverrouillé par {caller.DisplayName}.",
		} );

		caller.Notify( NotificationType.Success,
			locked ? "Serveur verrouillé : seuls les admins peuvent rejoindre." : "Serveur déverrouillé." );
	}

	[Rpc.Host]
	public static async void RPC_WhitelistAdd( string steamid64 )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		if ( string.IsNullOrWhiteSpace( steamid64 ) )
		{
			caller.Notify( NotificationType.Error, "SteamID64 invalide." );
			return;
		}

		var users = await ApiComponent.Instance.GetUserWhitelist();
		List<string> SteamIdWhitelist = new List<string>();
		foreach ( var user in users )
		{
			SteamIdWhitelist.Add( user.SteamId.ToString() );
		}
			
		var whitelistRealList = ServerManager.WhitelistedSteamId.Union( SteamIdWhitelist ).ToList();
		if ( whitelistRealList.Contains( steamid64 ) )
		{
			caller.Notify( NotificationType.Warning, $"Le SteamID {steamid64} est déjà dans la whitelist." );
			return;
		}

		await ApiComponent.Instance.AddPlayerInWhitelist( steamid64, caller.SteamId.ToString() );
		caller.Notify( NotificationType.Success, $"SteamID {steamid64} ajouté à la whitelist." );
	}

	[Rpc.Host]
	public static async void RPC_WhitelistRemove( string steamid64 )
	{
		Assert.True( Networking.IsHost );

		var caller = Rpc.Caller.GetClient();
		if ( !caller.IsAdmin )
		{
			caller.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		var success = await ApiComponent.Instance.RemoveWhiteListPlayer( steamid64, caller.SteamId.ToString() );
		if ( !success.Success )
		{
			caller.Notify( NotificationType.Warning, $"Le SteamID {steamid64} n'est pas dans la whitelist." );
			return;
		}

		caller.Notify( NotificationType.Success, $"SteamID {steamid64} retiré de la whitelist." );
	}

	[Rpc.Host]
	public static void RPC_HealPlayer( Client target, float amount )
	{
		Assert.True( Networking.IsHost );

		// Vérifications de sécurité de base
		if ( !target.IsValid() || target.PlayerPawn == null ) return;

		var caller = Rpc.Caller.GetClient();

		var healthComp = target.PlayerPawn.HealthComponent;
		if ( !healthComp.IsValid() ) return;

		if ( amount <= 0 )
		{
			// Soin complet si amount est -1 ou non précisé
			healthComp.Health = healthComp.MaxHealth;

			// On remet aussi les besoins au max pour un "Full Heal"
			if ( target.Data != null )
			{
				target.Data.Hunger = 100f;
				target.Data.Thirst = 100f;
			}

			//target.Notify( NotificationSystem.NotificationType.Success, "Vous avez été entièrement soigné." );
		}
		else
		{
			// Soin d'un montant précis sans dépasser le MaxHealth
			healthComp.Health = MathF.Min( healthComp.MaxHealth, healthComp.Health + amount );

			//target.Notify( NotificationSystem.NotificationType.Success, $"Vous avez reçu {amount} HP." );
		}

		// Notification à l'admin (caller) du succès de l'action
		if ( caller != null )
			caller.Notify( NotificationType.Success, $"[Admin] Heal appliqué sur {target.DisplayName} (Montant: {(amount <= 0 ? "MAX" : amount)})" );
	}
}
