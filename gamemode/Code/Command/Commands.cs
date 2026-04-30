using Facepunch;
using OpenFramework.ChatSystem;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;
using OpenFramework.Systems.Jobs;
using OpenFramework.UI.QuickMenuSystem;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Command;

public static partial class Commands
{
	[Command( "Admin Menu", ["admin"], icon: "ui/icons/admin.svg", permission: CommandPermission.Admin )]
	public static void AdminMenu()
	{
		if ( !Client.Local.IsValid() ) return;
		QuickMenu.OpenLocal<AdminActionMenu>();
	}

	[Command( "Give Money", ["givemoney"], icon: "ui/icons/money.svg", permission: CommandPermission.Admin )]
	public static void GiveMoney( [CommandArg( AutoResolve = true )] Client client, int amount )
	{
		if ( !client.IsValid() ) return;
		RPC_GiveMoney( client, amount );
	}

	[Command( "Give Bank Money", ["givebank"], icon: "ui/icons/bank.svg", permission: CommandPermission.Admin )]
	public static void GiveBankMoney( [CommandArg( AutoResolve = true )] Client client, int amount )
	{
		if ( !client.IsValid() ) return;
		RPC_GiveBankMoney( client, amount );
	}

	[Command( "Kick Player", ["kick"], "Kick a player by name or ID", "ui/icons/leave.svg", CommandPermission.Admin )]
	public static void Kick( [CommandArg( AutoResolve = true )] Client client, string reason = "Kicked by admin" )
	{
		if ( !client.IsValid() ) return;
		RPC_Kick( client, reason );
	}

	[Command( "Ban Player", ["ban"], "Ban a player by name or ID", "ui/icons/ban.svg", CommandPermission.Admin )]
	public static void Ban( [CommandArg( AutoResolve = true )] Client target, int duration, string reason = "Banned by admin" )
	{
		if ( !target.IsValid() ) return;
		RPC_Ban( target, duration, reason );
	}

	[Command( "Unban Player", ["unban"], "Unban a player by SteamID", "ui/icons/ban.svg", CommandPermission.Admin )]
	public static void Unban( ulong steamid )
	{
		RPC_Unban( steamid );
	}

	[Command( "Teleport To Player", ["tp", "teleportto"], "Teleport to another player", "ui/icons/goto.svg", CommandPermission.Admin )]
	public static void TeleportTo( [CommandArg( AutoResolve = true )] Client target )
	{
		if ( !target.IsValid() ) return;
		RPC_TeleportTo( target );
	}

	[Command( "Bring Player", ["bring", "teleport"], "Teleport a player to you", "ui/icons/teleport.svg", CommandPermission.Admin )]
	public static void Bring( [CommandArg( AutoResolve = true )] Client target )
	{
		if ( !target.IsValid() ) return;
		RPC_Bring( target );
	}

	[Command( "Noclip", ["noclip"], "Noclip self or a player", "ui/icons/noclip.svg", CommandPermission.Admin )]
	public static void Noclip( [CommandArg( AutoResolve = true )] Client target = null )
	{
		if ( !target.IsValid() ) return;
		RPC_Noclip( target );
	}

	[Command( "Give Item", ["giveitem"], "Give item to a player", "ui/icons/item.svg", CommandPermission.Admin )]
	public static void GiveItem( [CommandArg( AutoResolve = true )] Client target, string itemid, int quantity = 1 )
	{
		if ( !target.IsValid() ) return;
		RPC_GiveItem( target, itemid, quantity );
	}

	[Command( "Give Tools", ["givetools"], "Give tool to a player", "ui/icons/weapon.svg", CommandPermission.Admin )]
	public static void GiveTool( [CommandArg( AutoResolve = true )] Client target, string tools )
	{
		if ( !target.IsValid() ) return;
		RPC_GiveTool( target, tools );
	}

	[Command( "Remove Tools", ["removetools"], "Remove tool from a player", "ui/icons/weapon.svg", CommandPermission.Admin )]
	public static void RemoveTool( [CommandArg( AutoResolve = true )] Client target, string tools )
	{
		if ( !target.IsValid() ) return;
		RPC_RemoveTool( target, tools );
	}

	[Command( "Give Weapon", ["giveweapon"], "Give weapon to a player", "ui/icons/pistol.svg", CommandPermission.Admin )]
	public static void GiveWeapon( [CommandArg( AutoResolve = true )] Client target, string weapon )
	{
		if ( !target.IsValid() ) return;
		RPC_GiveWeapon( target, weapon );
	}

	[Command( "Mute player", ["mute"], "Allow to mute a player voice globally.", "ui/icons/mute.svg", CommandPermission.Admin )]
	public static void Mute( [CommandArg( AutoResolve = true )] Client target, float delay = -1 )
	{
		RPC_Mute( target, delay );
	}

	[Command( "Unmute player", ["unmute"], "Allow to unmute a muted player voice globally.", "ui/icons/unmute.svg", CommandPermission.Admin )]
	public static void Unmute( [CommandArg( AutoResolve = true )] Client target )
	{
		RPC_Unmute( target );
	}

	[Command( "Godmode", ["godmode"], "Enable/disable god mod on a player.", "ui/icons/godmode.svg", CommandPermission.Admin )]
	public static void Godmode( [CommandArg( AutoResolve = true )] Client target )
	{
		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			Client.Local.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}
		RPC_Godmode( target );
	}

	[Command( "Slap", ["slap"], "Slap a player.", "ui/icons/slap.svg", CommandPermission.Admin )]
	public static void Slap( [CommandArg( AutoResolve = true )] Client target, float damage = 5f )
	{
		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			Client.Local.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}
		RPC_Slap( target, damage );
	}

	[Command( "Freeze", ["freeze"], "Freeze/unfreeze a player.", "ui/icons/freeze.svg", CommandPermission.Admin )]
	public static void Freeze( [CommandArg( AutoResolve = true )] Client target, float delay = -1 )
	{
		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			Client.Local.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}
		RPC_Freeze( target, delay );
	}

	[Command( "Respawn", ["respawn"], "Respawn a player.", "ui/icons/respawn.svg", CommandPermission.Admin )]
	public static void Respawn( [CommandArg( AutoResolve = true )] Client target )
	{
		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State != LifeState.Dead )
		{
			Client.Local.Notify( NotificationType.Error, $"{target.DisplayName} est déjà en vie." );
			return;
		}
		// Revive sur place (sur le ragdoll), pas au random spawn metro de la
		// premiere connexion. RPC_Respawn passe par Client.Respawn() qui
		// utilise GetRandomSpawnPoint en attendant un SpawnAtLastPosition
		// jamais emis dans le contexte d'un /respawn admin.
		RPC_RespawnInPlace( target );
	}

	[Command( "Kill", ["kill"], "Tue un joueur.", "ui/icons/slap.svg", CommandPermission.Admin )]
	public static void Kill( [CommandArg( AutoResolve = true )] Client target )
	{
		var pawn = target.PlayerPawn;
		if ( pawn == null || !pawn.IsValid || pawn.HealthComponent.State == LifeState.Dead )
		{
			Client.Local.Notify( NotificationType.Error, $"{target.DisplayName} n'est pas en vie." );
			return;
		}
		RPC_Kill( target );
	}

	// -------------------------------------------------------------------------
	// Commandes publiques (Everyone)
	// -------------------------------------------------------------------------

	[Command( "Thirdperson", ["3rd", "thirdperson"], "Change camera to thirdperson view", "ui/icons/camera.svg", CommandPermission.Admin )]
	public static void Thirdperson()
	{
		var cl = Client.Local;
		if ( !cl.IsValid() || !cl.IsAdmin ) return;
		var pawn = cl.PlayerPawn;
		if ( pawn == null ) return;
		pawn.CameraController.Mode = pawn.CameraController.Mode == CameraMode.FirstPerson
			? CameraMode.ThirdPerson
			: CameraMode.FirstPerson;
	}

	[Command( "Whisper", ["whisper", "private", "sendto"], "Envoyer un chuchotement aux joueurs à proximité.", "ui/icons/message.svg" )]
	public static void Whisper( string message, float radius = 4f )
	{
		var sender = Client.Local;
		if ( sender == null || sender.Pawn == null ) return;
		if ( string.IsNullOrWhiteSpace( message ) ) return;

		var origin = sender.Pawn.WorldPosition;
		var recipients = GameUtils.AllPlayers
			.Where( c => c?.Pawn != null && c != sender && c.Pawn.WorldPosition.Distance( origin ) <= radius )
			.Select( c => c.Connection )
			.ToArray();

		if ( recipients.Length == 0 )
		{
			sender.Notify( NotificationType.Info, "Personne assez proche pour vous entendre chuchoter." );
			return;
		}

		using ( Rpc.FilterInclude( recipients ) )
		{
			ChatUI.Receive( new() { AuthorId = sender.Id, AuthorName = sender.DisplayName, Message = $"[chuchote] {message}" } );
		}

		ChatUI.Receive( new() { AuthorId = sender.Id, AuthorName = sender.DisplayName, Message = $"[à proximité] {message}" } );
	}

	[Command( "DM", ["dm", "pm", "message"], "Envoyer un message privé à un joueur.", "ui/icons/message.svg" )]
	public static void DM( [CommandArg( AutoResolve = true )] Client target, string message )
	{
		var sender = Client.Local;
		if ( sender == null || target == null || target.Connection == null ) return;
		if ( string.IsNullOrWhiteSpace( message ) ) return;

		using ( Rpc.FilterInclude( target.Connection ) )
		{
			ChatUI.Receive( new() { AuthorId = sender.Id, AuthorName = sender.DisplayName, Message = $"[DM de {sender.DisplayName}] {message}" } );
		}

		ChatUI.Receive( new() { AuthorId = sender.Id, AuthorName = sender.DisplayName, Message = $"[DM à {target.DisplayName}] {message}" } );
	}

	[Command( "SetJob", ["setjob"], "Assigne un job à un joueur", "ui/icons/briefcase.svg", CommandPermission.Admin )]
	public static void SetJob( [CommandArg( AutoResolve = true )] Client target, string jobname, string gradename = "" )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;

		var job = JobSystem.GetJob( jobname );
		if ( job == null )
		{
			admin.Notify( NotificationType.Error, $"Métier '{jobname}' introuvable." );
			return;
		}

		if ( !string.IsNullOrEmpty( gradename ) && job.HasGrades )
		{
			var grade = job.Grades?.FirstOrDefault( g => g.Name.Equals( gradename, StringComparison.OrdinalIgnoreCase ) );
			if ( grade == null )
			{
				admin.Notify( NotificationType.Error, $"Le grade '{gradename}' n'existe pas dans {job.DisplayName}." );
				return;
			}
		}

		JobSystem.SetJob( target, jobname, gradename );
		admin.Notify( NotificationType.Success,
			$"Vous avez mis {target.DisplayName} en {job.DisplayName}{(string.IsNullOrEmpty( gradename ) ? "" : $" ({gradename})")}" );
		target.Notify( NotificationType.Info, "Votre métier a été modifié par un administrateur." );
	}

	[Command( "Jail", ["jail"], "Met un joueur en détention", "ui/icons/lock.svg", CommandPermission.Admin )]
	public static void Jail( [CommandArg( AutoResolve = true )] Client target, int duration, string reason = "" )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;
		if ( duration <= 0 )
		{
			admin.Notify( NotificationType.Error, "La durée doit être supérieure à 0." );
			return;
		}
		// RPC_Jail( target, duration, reason );
	}

	[Command( "SpawnVehicleAndSeat", ["spawnvehseat"], "Spawn un véhicule et met le joueur dedans", "ui/icons/car_rental.svg", CommandPermission.Admin )]
	public static void SpawnVehicle( [CommandArg( AutoResolve = true )] Client target, string vehicleResourcePath, bool autoEnter = true )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;
		if ( string.IsNullOrEmpty( vehicleResourcePath ) )
		{
			admin.Notify( NotificationType.Error, "Aucun véhicule spécifié." );
			return;
		}
		// RPC_SpawnVehicle( target, vehicleResourcePath, autoEnter );
		admin.Notify( NotificationType.Info, $"🚗 {target.DisplayName} mis dans un véhicule." );
	}

	[Command( "Spawn Car", ["spawncar"], "Spawn un véhicule devant vous", "ui/icons/car_rental.svg" )]
	public static void SpawnCar()
	{
		if ( !Client.Local.IsAdmin )
		{
			Client.Local.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		RPC_SpawnCar();
	}

	[Command( "Spawn Trailer", ["spawntrailer"], "Spawn une remorque devant vous", "ui/icons/car_rental.svg" )]
	public static void SpawnTrailer()
	{
		if ( !Client.Local.IsAdmin )
		{
			Client.Local.Notify( NotificationType.Error, "Vous n'avez pas accès a cette commande !" );
			return;
		}

		RPC_SpawnTrailer();
	}

	[Command( "SetHunger", ["hunger"], "Modifie la faim d'un joueur", "ui/icons/restaurant.svg", CommandPermission.Admin )]
	public static void SetHunger( [CommandArg( AutoResolve = true )] Client target, float amount )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;
		RPC_AddHunger( target, amount );
		admin.Notify( NotificationType.Info, $"🍴 Faim de {target.DisplayName} modifiée de {amount}." );
	}

	[Command( "SetThirst", ["thirst"], "Modifie la soif d'un joueur", "ui/icons/water_drop.svg", CommandPermission.Admin )]
	public static void SetThirst( [CommandArg( AutoResolve = true )] Client target, float amount )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;
		RPC_AddThirst( target, amount );
		admin.Notify( NotificationType.Info, $"💧 Soif de {target.DisplayName} modifiée de {amount}." );
	}

	[Command( "Heal", ["hp", "health"], "Soigne un joueur d'un montant précis (ou max par défaut)", "ui/icons/health_and_safety.svg", CommandPermission.Admin )]
	public static void Heal( [CommandArg( AutoResolve = true )] Client target, float amount = -1 )
	{
		var admin = Client.Local;
		if ( admin == null || target == null ) return;
		RPC_HealPlayer( target, amount );
	}

	[Command( "Minimap Players", ["minimap"], "Affiche/masque les joueurs sur votre minimap (admin)", "ui/icons/admin.svg", CommandPermission.Admin )]
	public static void ToggleMinimapPlayers()
	{
		PlayerMarker.ShowPlayersOnMinimap = !PlayerMarker.ShowPlayersOnMinimap;
		var state = PlayerMarker.ShowPlayersOnMinimap ? "activés" : "désactivés";
		Client.Local.Notify( NotificationType.Info, $"Icônes joueurs sur la minimap : {state}" );
	}

	[Command( "Lock Server", ["lock", "lockserver"], "Verrouille le serveur : seuls les admins peuvent rejoindre.", "ui/icons/lock.svg", CommandPermission.Admin )]
	public static void LockServer()
	{
		var admin = Client.Local;
		if ( admin == null ) return;
		RPC_SetServerLock( true );
	}

	[Command( "Unlock Server", ["unlock", "unlockserver"], "Deverrouille le serveur : tout le monde peut rejoindre.", "ui/icons/lock.svg", CommandPermission.Admin )]
	public static void UnlockServer()
	{
		var admin = Client.Local;
		if ( admin == null ) return;
		RPC_SetServerLock( false );
	}

	[Command( "Whitelist Add", ["wladd", "whitelistadd"], "Ajoute un SteamID64 à la whitelist", "ui/icons/admin.svg", CommandPermission.Admin )]
	public static void WhitelistAdd( string steamid64 )
	{
		var admin = Client.Local;
		if ( admin == null ) return;
		RPC_WhitelistAdd( steamid64 );
	}

	[Command( "Whitelist Remove", ["wlremove", "whitelistremove"], "Retire un SteamID64 de la whitelist", "ui/icons/admin.svg", CommandPermission.Admin )]
	public static void WhitelistRemove( string steamid64 )
	{
		var admin = Client.Local;
		if ( admin == null ) return;
		RPC_WhitelistRemove( steamid64 );
	}
}
