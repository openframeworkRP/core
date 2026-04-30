using Sandbox.Diagnostics;
using Sandbox.Events;
using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems.Pawn;
using OpenFramework.Systems.Weapons;
using OpenFramework.Systems.Weapons.Interfaces;
using OpenFramework.UI;
using OpenFramework.Utility;
using static Facepunch.NotificationSystem;

namespace Facepunch;

public record EquipmentDroppedEvent( DroppedEquipment Dropped, PlayerPawn Player ) : IGameEvent;
public record EquipmentPickedUpEvent( PlayerPawn Player, DroppedEquipment Dropped, Equipment Equipment ) : IGameEvent;

public partial class DroppedEquipment : Component, Component.ICollisionListener, IMarkerObject, IUse
{
	[Property] public EquipmentResource Resource { get; set; }

	[Property, RequireComponent]
	public Rigidbody Rigidbody { get; private set; }

	/// <summary>
	/// Creates a world instance of a dropped piece of equipment. Takes in a <see cref="EquipmentResource"/>, position, rotation, and optionally a held weapon to inherit data from.
	/// </summary>
	/// <param name="resource"></param>
	/// <param name="position"></param>
	/// <param name="rotation"></param>
	/// <param name="heldWeapon"></param>
	/// <param name="networkSpawn"></param>
	/// <returns></returns>
	public static DroppedEquipment Create( EquipmentResource resource, Vector3 position, Rotation? rotation = null, Equipment heldWeapon = null, bool networkSpawn = true )
	{
		Assert.True( Networking.IsHost );

		// 1. On clone la prefab de base (qui contient déjà BoxCollider Trigger, Rigidbody et les Tags)
		// Assure-toi que le tag "actionmenu" est bien SAUVEGARDÉ dans cette prefab.
		var prefab = GameObject.GetPrefab( "prefabs/dropped_weapon_base.prefab" );
		var go = prefab.Clone( position, rotation ?? Rotation.Identity );

		go.Name = resource.Name;

		// 2. Configuration du script et de la ressource
		var droppedWeapon = go.Components.Get<DroppedEquipment>();
		droppedWeapon.Resource = resource;

		var modelCollider = go.Components.Get<ModelCollider>();
		if ( modelCollider.IsValid() )
		{
			modelCollider.Model = resource.WorldModel;
		}

		// 3. Configuration visuelle dynamique
		var renderer = go.Components.Get<SkinnedModelRenderer>();
		renderer.Model = resource.WorldModel;
		renderer.BodyGroups |= resource.WorldModelBodyGroups;

		// 4. Ajustement du BoxCollider (Trigger) pour la détection
		var collider = go.Components.Get<BoxCollider>();
		if ( collider.IsValid() )
		{
			var bounds = resource.WorldModel.Bounds;
			collider.Center = bounds.Center;
			collider.Scale = bounds.Size;
			collider.IsTrigger = true; // Indispensable pour ton UpdateUse
		}

		// 5. Transfert d'état si l'arme était tenue (munitions, etc.)
		if ( heldWeapon is not null )
		{
			foreach ( var state in heldWeapon.GetComponentsInChildren<IDroppedWeaponState>( true ) )
			{
				state.CopyToDroppedWeapon( droppedWeapon );
			}
		}

		// 6. Spawn réseau
		if ( networkSpawn )
		{
			go.NetworkSpawn();
		}

		// 7. Dispatch de l'événement pour les autres systèmes (Ex: Killfeed ou logs)
		Game.ActiveScene.Dispatch( new EquipmentDroppedEvent( droppedWeapon, heldWeapon?.Owner ) );

		return droppedWeapon;
	}

	public UseResult CanUse( PlayerPawn player )
	{
		var container = player?.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container == null || container.GetFirstFreeSlot() < 0 )
			return "Inventaire plein";
		return true;
	}

	private bool _isUsed;

	public void OnUse( PlayerPawn player )
	{
		if ( !Networking.IsHost || player == null ) return;
		if ( _isUsed ) return;
		_isUsed = true;

		if ( !player.IsValid() )
			return;

		var container = player.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndChildren );
		if ( container == null )
		{
			_isUsed = false;
			return;
		}

		var meta = ItemMetadata.All.FirstOrDefault( m => m.IsWeapon && m.WeaponResource == Resource );
		if ( meta == null )
		{
			Log.Warning( $"[Pickup] Aucun ItemMetadata weapon trouve pour la resource '{Resource?.ResourceName}'." );
			_isUsed = false;
			return;
		}

		int slot = container.GetFirstFreeSlot();
		if ( slot < 0 )
		{
			_isUsed = false;
			return;
		}

		var go = new GameObject( true );
		go.Parent = container.GameObject;
		go.Name = $"Item_{meta.Name}";

		var newItem = go.Components.Create<InventoryItem>();
		newItem.Metadata = meta;
		newItem.SlotIndex = slot;
		newItem.Quantity = 1;

		go.NetworkSpawn();

		// Restaure l'etat du chargeur/munitions directement dans les attributs de l'item
		// (APRES NetworkSpawn pour ne pas etre ecrase par OnStart qui copie les defaults du Metadata).
		var ammoState = Components.Get<WeaponAmmoDroppedState>();
		if ( ammoState != null )
		{
			newItem.Attributes["loaded_mag_type"] = ammoState.LoadedMagType ?? "";
			newItem.Attributes.SetInt( "loaded_mag_ammo", ammoState.LoadedMagAmmo );
			newItem.Attributes.SetInt( "loaded_mag_capacity", ammoState.LoadedMagCapacity );
			newItem.Attributes.SetInt( "primary_ammo", ammoState.PrimaryAmmo );
			Log.Info( $"[Pickup] Etat restaure: type='{ammoState.LoadedMagType}', ammo={ammoState.LoadedMagAmmo}/{ammoState.LoadedMagCapacity}, primary={ammoState.PrimaryAmmo}" );
		}

		container.MarkDirty();

		Log.Info( $"[Pickup] Arme '{meta.ResourceName}' ajoutee a l'inventaire (slot {slot})" );

		player.Client?.Notify( NotificationType.Success, $"+{meta.Name}" );

		Game.ActiveScene.Dispatch( new EquipmentPickedUpEvent( player, this, null ) );

		GameObject.Destroy();
	}
	
	/*void ICollisionListener.OnCollisionStart( Collision collision )
	{
		Log.Info( collision );

		if ( !Networking.IsHost ) return;

		// Conna: this is longer than Daenerys Targaryen's full title.
		if ( collision.Other.GameObject.Root.GetComponentInChildren<PlayerPawn>() is { } player )
		{
			// Don't pickup weapons if we're dead.
			if ( player.HealthComponent.State != LifeState.Alive )
				return;
			if ( player.Inventory.CanTake( Resource ) != PlayerInventory.PickupResult.Pickup )
				return;

			// Don't auto-pickup if we already have a weapon in this slot.
			if ( player.Inventory.HasInSlot( Resource.Slot ) )
				return;

			OnUse( player );
		}
	}*/

	/// <summary>
	/// Where is the marker?
	/// </summary>
	
	Vector3 IMarkerObject.MarkerPosition => WorldPosition + Vector3.Up * 8f;

	/// <summary>
	/// What text?
	/// </summary>
	string IMarkerObject.DisplayText => string.Empty;

	float IMarkerObject.MarkerMaxDistance => 70f;

	string IMarkerObject.InputHint => string.Empty;

	bool IMarkerObject.LookOpacity => false;
	
}
