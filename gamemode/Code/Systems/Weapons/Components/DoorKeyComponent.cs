using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.World;
using static Facepunch.NotificationSystem;

namespace OpenFramework.Systems.Weapons;

[Icon( "key" )]
[Title( "Door Key" ), Group( "Weapon Components" )]
public sealed class DoorKeyComponent : EquipmentComponent
{
	[Property] public float MaxRange { get; set; } = 150f;

	private TimeSince _timeSinceAction;

	protected override void OnFixedUpdate()
	{
		if ( !Equipment.IsDeployed ) return;
		if ( !Equipment.Owner.IsLocallyControlled ) return;
		if ( _timeSinceAction < 0.3f ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			_timeSinceAction = 0f;
			TryAction( locked: true );
		}
		else if ( Input.Pressed( "attack2" ) )
		{
			_timeSinceAction = 0f;
			TryAction( locked: false );
		}
	}

	private void TryAction( bool locked )
	{
		var player = Equipment.Owner;
		var start = player.AimRay.Position;
		var end = start + player.AimRay.Forward * MaxRange;

		var tr = Scene.Trace.Ray( start, end )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "trigger" )
			.Run();

		if ( !tr.Hit || tr.GameObject == null ) return;

		var door = tr.GameObject.Components.GetInAncestorsOrSelf<Door>();
		if ( door == null ) return;

		SetDoorLock( door, Equipment.LinkedItem, locked );
	}

	[Rpc.Host]
	private void SetDoorLock( Door door, InventoryItem keyItem, bool locked )
	{
		if ( !Networking.IsHost ) return;
		if ( door == null || !door.IsValid ) return;

		var caller = Rpc.Caller.GetClient();
		if ( caller == null ) return;

		if ( keyItem == null || !keyItem.IsValid )
		{
			caller.Notify( NotificationType.Error, "Clé introuvable dans l'inventaire." );
			return;
		}

		if ( !keyItem.Attributes.TryGetValue( InventoryItem.AttrDoorGuids, out var guidsStr )
			|| string.IsNullOrEmpty( guidsStr ) )
		{
			caller.Notify( NotificationType.Error, "Cette clé n'est associée à aucune porte." );
			return;
		}

		if ( !guidsStr.Split( ',' ).Contains( door.GameObject.Id.ToString() ) )
		{
			caller.Notify( NotificationType.Error, "Cette clé ne correspond pas à cette porte." );
			return;
		}

		bool hasAccess = door.Owner == caller || (door.CoOwners?.Contains( caller ) ?? false);
		if ( !hasAccess )
		{
			keyItem.GameObject.Destroy();
			var container = caller.PlayerPawn?.GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
			container?.MarkDirty();
			caller.Notify( NotificationType.Warning, "Cette clé n'est plus valide et a été supprimée." );
			Log.Info( $"[DoorKey] Clé invalide supprimée pour {caller.DisplayName} (porte '{door.GameObject.Name}')" );
			return;
		}

		Log.Info( $"[DoorKey] {caller.DisplayName} utilise sa clé sur '{door.GameObject.Name}' (locked={locked})" );

		if ( locked )
			Door.Lock( door );
		else
			Door.Unlock( door );
	}
}
