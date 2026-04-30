using Sandbox.Diagnostics;
using OpenFramework.ChatSystem;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Weapons;
using System.Threading.Tasks;
using static Facepunch.NotificationSystem;
using static OpenFramework.ChatSystem.ChatUI;

namespace OpenFramework.Utility;

public static class ActionGraphUtility
{
	private static bool IsDebug => GameLoop.Constants.DebugMode();

	/// <summary>
	/// Sends a chat message to a specific client.
	/// </summary>
	[ActionGraphNode( "core.print" )]
	[Title( "Print To Chat" ), Group( "OpenFramework/Chat" ), Icon( "chat" )]
	public static void PrintToChat( Client target, string message )
	{
		Assert.True( Networking.IsHost );
		if ( target == null ) return;

		if ( IsDebug )
			Log.Info( $"[Chat] Printing to {target.DisplayName}: {message}" );

		using ( Rpc.FilterInclude( c => c == target.Connection ) )
		{
			var obj = new ChatMessage { HostMessage = true, Message = message };
			ChatUI.Receive( obj );
		}
	}

	/// <summary>
	/// Adds hunger to the client. Returns True if the action succeeded.
	/// </summary>
	[ActionGraphNode( "core.add_hunger" )]
	[Title( "Add Hunger" ), Group( "OpenFramework/Player" ), Icon( "restaurant" )]
	public static bool AddHunger( Client target, float min, float max )
	{
		// Seul l'hôte est autorisé à modifier les stats de faim
		Assert.True( Networking.IsHost );

		if ( target?.Data == null )
		{
			Log.Warning( $"[AddHunger] ECHEC — target={target?.DisplayName ?? "NULL"}, Data=null" );
			return false;
		}

		float before = target.Data.Hunger;
		Log.Info( $"[AddHunger] {target.DisplayName} | Faim actuelle : {before:0.#}% | Plage item : [{min:0.#} – {max:0.#}]" );

		// Calcul de la valeur nutritive aléatoire entre le min et le max
		float amount = Game.Random.Float( min, max );

		// Si le joueur est déjà totalement rassasié (100%), on annule l'action
		if ( target.Data.Hunger >= 100f )
		{
			Log.Info( $"[AddHunger] {target.DisplayName} : déjà à 100%, rien à restaurer." );
			return false;
		}

		// On ajoute la nourriture. Le surplus est automatiquement ignoré grâce au Clamp à 100.
		target.Data.Hunger = Math.Clamp( target.Data.Hunger + amount, 0f, 100f );
		target.Notify(NotificationType.Success, $"Vous avez mangé" );

		Log.Info( $"[AddHunger] {target.DisplayName} | Donné : +{amount:0.##} | {before:0.#}% → {target.Data.Hunger:0.#}%" );

		return true;
	}

	[ActionGraphNode( "core.add_thirst" )]
	[Title( "Add Thirst" ), Group( "OpenFramework/Player" ), Icon( "local_drink" )]
	public static bool AddThirst( Client target, float min, float max )
	{
		// Seul l'hôte peut modifier les besoins des joueurs
		Assert.True( Networking.IsHost );

		if ( target?.Data == null )
		{
			Log.Warning( $"[AddThirst] ECHEC — target={target?.DisplayName ?? "NULL"}, Data=null" );
			return false;
		}

		float before = target.Data.Thirst;
		Log.Info( $"[AddThirst] {target.DisplayName} | Soif actuelle : {before:0.#}% | Plage item : [{min:0.#} – {max:0.#}]" );

		// Génération de la quantité à ajouter
		float amount = Game.Random.Float( min, max );

		// On vérifie si le joueur est déjà au maximum avant d'ajouter
		if ( target.Data.Thirst >= 100f )
		{
			Log.Info( $"[AddThirst] {target.DisplayName} : déjà hydraté à 100%, rien à restaurer." );
			return false;
		}

		// On ajoute la quantité sans restriction de surplus, mais on bride le résultat final entre 0 et 100
		target.Data.Thirst = Math.Clamp( target.Data.Thirst + amount, 0f, 100f );
		target.Notify( NotificationType.Success, $"Vous avez bu" );

		Log.Info( $"[AddThirst] {target.DisplayName} | Donné : +{amount:0.##} | {before:0.#}% → {target.Data.Thirst:0.#}%" );

		return true;
	}

	/// <summary>
	/// Spawns a residue prefab at the player's location using its prefab path.
	/// </summary>
	[ActionGraphNode( "core.spawn_trash" )]
	[Title( "Spawn Trash" ), Group( "OpenFramework/Inventory" ), Icon( "delete" )]
	public static void SpawnTrash( PlayerPawn player, string trashPrefabPath )
	{
		Assert.True( Networking.IsHost );

		// On vérifie que le chemin n'est pas vide et que le joueur est valide
		if ( player == null || string.IsNullOrEmpty( trashPrefabPath ) ) return;

		if ( IsDebug ) Log.Info( $"[Inventory] Spawning trash from path: {trashPrefabPath}" );

		// On prépare le transform (position du joueur + 10 unités vers le haut)
		var spawnTransform = new Transform( player.WorldPosition + Vector3.Up * 10, Rotation.Random );

		// Appel de ton utilitaire maison pour le spawn réseau
		var trash = Spawnable.CreateWithReturnFromHost( trashPrefabPath, spawnTransform );

		if ( trash != null )
		{
			// On donne une petite impulsion physique pour le réalisme
			if ( trash.Components.TryGet<Rigidbody>( out var rb ) )
			{
				rb.Velocity = (Vector3.Random + Vector3.Up) * 50f;
			}
		}
		else if ( IsDebug )
		{
			Log.Warning( $"[Inventory] Failed to spawn trash from path: {trashPrefabPath}. Check if the path is correct." );
		}
	}

	/// <summary>
	/// Server-side timer for item actions. Triggers UI on client and waits before returning success.
	/// </summary>
	[ActionGraphNode( "ui.item_action_progress" )]
	[Title( "Item Action Progress" ), Group( "OpenFramework/Inventory" ), Icon( "hourglass_bottom" )]
	public static async Task<bool> ItemActionProgress( Client target, string label, float duration )
	{
		Assert.True( Networking.IsHost );
		if ( duration <= 0 ) return true;

		if ( IsDebug ) Log.Info( $"[Server] Starting Progress Timer for {target.DisplayName}: {duration}s" );

		// 1. On demande au client d'afficher son UI via RPC
		using ( Rpc.FilterInclude( c => c == target.Connection ) )
		{
			ItemActionProgressBar.StartProgress( duration, label );
			// Note: Ici on ne fait pas 'await' car on veut que le serveur continue
		}

		// 2. Le serveur attend réellement le temps imparti
		// C'est ici que la sécurité réside.
		await Task.Delay( (int)(duration * 1000) );

		if ( IsDebug ) Log.Info( $"[Server] Progress Finished for {target.DisplayName}" );

		return true;
	}

	/// <summary>
	/// Donne une arme au joueur et initialise ses munitions à partir des attributs de l'item.
	/// </summary>
	[ActionGraphNode( "core.player.give_weapon" )]
	[Title( "Give Weapon" ), Group( "OpenFramework/Player" ), Icon( "sports_martial_arts" )]
	public static bool GiveWeapon( InventoryItem item, PlayerPawn target, EquipmentResource weapon )
	{
		Assert.True( Networking.IsHost );

		if ( target == null || weapon == null || item == null )
			return false;

		// 1. On fait apparaître l'objet physique
		var equipment = target.Inventory.Give( weapon );

		if ( equipment != null )
		{
			// 2. ON ETABLIT LE LIEN (Le plus important)
			// C'est ce qui permettra au OnStart de WeaponAmmo de fonctionner
			equipment.LinkedItem = item;

			// 3. On s'assure que le composant Ammo a aussi la référence
			// (Au cas où il n'attendrait pas le OnDeployed pour lire l'item)
			if ( equipment.GetComponentInChildren<WeaponAmmo>() is { } ammo )
			{
				ammo.LinkedItem = item;
				// OnDeployed s'est deja execute avec LinkedItem=null (item assigne apres Give()),
				// donc Ammo/MaxAmmo/MagPresent sont a 0. On re-lit les attributs maintenant.
				ammo.RefreshAmmoFromAttributes();
				ammo.MagPresent = ammo.HasMagazine;
			}

			if ( GameLoop.Constants.Instance.Debug )
				Log.Info( $"[Inventory] {weapon.ResourceName} lié à l'item {item.Id}." );
		}

		return true;
	}

	/// <summary>
	/// Retrieves the ItemMetadata resource by its name.
	/// </summary>
	[ActionGraphNode( "inventory.get_metadata_by_name" ), Pure]	[Title( "Get Item Metadata By Name" ), Group( "OpenFramework/Utility" ), Icon( "manage_search" )]
	public static ItemMetadata GetItemMetadataByName( string resourceName )
	{
		var meta = ItemMetadata.All.FirstOrDefault( x => x.ResourceName == resourceName );

		if ( IsDebug )
			Log.Info( $"[Inventory] Metadata Search '{resourceName}': {(meta != null ? "FOUND" : "NOT FOUND")}" );

		return meta;
	}

	/// <summary>
	/// Retrieves a EquipmentResource by its name.
	/// </summary>
	[ActionGraphNode( "inventory.get_weapon_resource_by_name" ), Pure]
	[Title( "Get Weapon Resource By Name" ), Group( "OpenFramework/Utility" ), Icon( "swords" )]
	public static EquipmentResource GetEquipmentResourceByName( string resourceName )
	{
		// Recherche parmi toutes les ressources de type EquipmentResource chargées
		var weapon = EquipmentResource.All.FirstOrDefault( x => x.ResourceName == resourceName );

		if ( GameLoop.Constants.Instance.Debug )
		{
			Log.Info( $"[Inventory] Searching EquipmentResource '{resourceName}': {(weapon != null ? "FOUND" : "NOT FOUND")}" );
		}

		return weapon;
	}

	/// <summary>
	/// Retrieves a SoundEvent resource by its name.
	/// </summary>
	[ActionGraphNode( "inventory.get_sound_by_name" ), Pure]
	[Title( "Get Sound By Name" ), Group( "OpenFramework/Utility" ), Icon( "volume_up" )]
	public static SoundEvent GetSoundByName( string soundName )
	{
		// Recherche parmi tous les SoundEvents chargés dans le projet
		//var sound = SoundEvent.All.FirstOrDefault( x => x.ResourceName == soundName );
		var sound = ResourceLibrary.Get<SoundEvent>( soundName );

		if ( GameLoop.Constants.Instance.Debug )
		{
			Log.Info( $"[Inventory] Searching SoundEvent '{soundName}': {(sound != null ? "FOUND" : "NOT FOUND")}" );
		}

		return sound;
	}

	// --- UI Notifications ---

	/// <summary>
	/// Sends a generic notification to the target client.
	/// </summary>
	[ActionGraphNode( "core.ui.notify_generic" )]
	[Title( "Notify Generic" ), Group( "OpenFramework/UI" ), Icon( "notifications" )]
	public static void NotifyGeneric( Client target, string message )
		=> target?.Notify( NotificationType.Generic, message );

	/// <summary>
	/// Sends an info notification to the target client.
	/// </summary>
	[ActionGraphNode( "core.notify_info" )]
	[Title( "Notify Info" ), Group( "OpenFramework/UI" ), Icon( "info" )]
	public static void NotifyInfo( Client target, string message )
		=> target?.Notify( NotificationType.Info, message );

	/// <summary>
	/// Sends a success notification to the target client.
	/// </summary>
	[ActionGraphNode( "core.notify_success" )]
	[Title( "Notify Success" ), Group( "OpenFramework/UI" ), Icon( "check_circle" )]
	public static void NotifySuccess( Client target, string message )
		=> target?.Notify( NotificationType.Success, message );

	/// <summary>
	/// Sends a warning notification to the target client.
	/// </summary>
	[ActionGraphNode( "core.notify_warning" )]
	[Title( "Notify Warning" ), Group( "OpenFramework/UI" ), Icon( "warning" )]
	public static void NotifyWarning( Client target, string message )
		=> target?.Notify( NotificationType.Warning, message );

	/// <summary>
	/// Sends an error notification to the target client.
	/// </summary>
	[ActionGraphNode( "core.notify_error" )]
	[Title( "Notify Error" ), Group( "OpenFramework/UI" ), Icon( "report" )]
	public static void NotifyError( Client target, string message )
		=> target?.Notify( NotificationType.Error, message );
}
