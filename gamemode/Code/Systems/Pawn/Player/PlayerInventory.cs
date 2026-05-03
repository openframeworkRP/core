using Facepunch;
using Sandbox.Diagnostics;
using OpenFramework.Command;
using OpenFramework.Extension;
using OpenFramework.Systems.Weapons;
using OpenFramework.World;

namespace OpenFramework.Systems.Pawn;

/// <summary>
/// The player's inventory.
/// </summary>
public partial class PlayerInventory : Component
{
	[RequireComponent] PlayerPawn Player { get; set; }

	private TimeSince _atmCheckTimer;
	private bool _cachedAnyAtmOpen;

	/// <summary>
	/// What equipment do we have right now?
	/// </summary>
	public IEnumerable<Equipment> Equipment => Player.GetComponentsInChildren<Equipment>();

	/// <summary>
	/// A <see cref="GameObject"/> that will hold all of our equipment.
	/// </summary>
	[Property] public GameObject WeaponGameObject { get; set; }

	/// <summary>
	/// Can we unequip the current weapon so we have no equipment out?
	/// </summary>
	[Property] public bool CanUnequipCurrentWeapon { get; set; } = false;

	/// <summary>
	/// Does this player have a defuse kit?
	/// </summary>
	public bool HasDefuseKit
	{
		get => Player.Client.Loadout.HasDefuseKit;
		set => Player.Client.Loadout.HasDefuseKit = value;
	}

	/// <summary>
	/// Gets the player's current weapon.
	/// </summary>
	private Equipment Current => Player.CurrentEquipment;

	/// <summary>
	/// Save last player's current Weapon
	/// </summary>
	//private Equipment LastWeaponBeforeBuild;

	public void Clear()
	{
		if ( !Networking.IsHost )
			return;

		foreach ( var wpn in Equipment )
		{
			wpn.GameObject.Destroy();
			wpn.Enabled = false;
		}
	}

	/// <summary>
	/// Déséquipe une arme depuis le client (appelé par le menu contextuel de l'inventaire).
	/// </summary>
	[Rpc.Host]
	public static void RPC_UnequipWeapon( Equipment equipment )
	{
		if ( !Networking.IsHost || equipment == null || !equipment.IsValid() ) return;

		var owner = equipment.Owner;
		if ( owner == null || !owner.IsValid() ) return;

		owner.Inventory.RemoveWeapon( equipment );
	}

	[Rpc.Owner( NetFlags.HostOnly )]
	public void RefillAmmo()
	{
		foreach ( var wpn in Equipment )
		{
			if ( wpn.GetComponentInChildren<WeaponAmmo>() is { } ammo )
			{
				// Recharge le chargeur chargé en remplissant son sous-conteneur
				if ( ammo.LinkedItem == null || !ammo.HasMagazine ) continue;
				ammo.LinkedItem.Attributes.SetInt( "loaded_mag_ammo", ammo.MaxAmmo );
				ammo.RefreshAmmoFromAttributes();
			}
		}
	}

	/// <summary>
	/// Try to drop the given held equipment item.
	/// </summary>
	/// <param name="weapon">Item to drop.</param>
	/// <param name="forceRemove">If we can't drop, remove it from the inventory anyway.</param>
	public void Drop( Equipment weapon, bool forceRemove = false )
	{
		using ( Rpc.FilterInclude( Connection.Host ) )
		{
			DropHost( weapon, forceRemove );
		}
	}

	//[Rpc.Broadcast]
	[Rpc.Host]
	private void DropHost( Equipment weapon, bool forceRemove )
	{
		if ( !Networking.IsHost || !weapon.IsValid() )
			return;

		// Les slots non droppables (items système, poings)
		var slot = weapon.Resource.Slot;
		if ( slot == EquipmentSlot.Punch || slot == EquipmentSlot.Handheld )
			return;

		var dropper = Game.ActiveScene.GetComponentInChildren<EquipmentDropper>();
		var canDrop = (dropper != null && dropper.CanDrop( Player, weapon ))
			|| slot == EquipmentSlot.Throwable
			|| slot == EquipmentSlot.Melee;

		if ( canDrop )
		{
			var tr = Scene.Trace
				.Ray( new Ray( Player.AimRay.Position, Player.AimRay.Forward ), 128 )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.WithoutTags( "trigger" )
				.Run();

			var position = tr.Hit
				? tr.HitPosition + tr.Normal * weapon.Resource.WorldModel.Bounds.Size.Length
				: Player.AimRay.Position + Player.AimRay.Forward * 32f;

			var rotation = Rotation.From( 0, Player.EyeAngles.yaw + 90, 90 );
			var dropped = DroppedEquipment.Create( weapon.Resource, position, rotation, weapon );

			if ( !tr.Hit )
			{
				dropped.Rigidbody.Velocity = Player.CharacterController.Velocity + Player.AimRay.Forward * 200f + Vector3.Up * 50f;
				dropped.Rigidbody.AngularVelocity = Vector3.Random * 8f;
			}
		}

		if ( canDrop || forceRemove )
		{
			// L'etat (chargeur, etc.) a deja ete copie sur le DroppedEquipment via
			// IDroppedWeaponState. On detruit l'InventoryItem source pour eviter
			// que l'arme reste dans l'inventaire du joueur apres avoir ete jetee.
			var linked = weapon.LinkedItem;
			if ( linked != null && linked.IsValid )
			{
				Log.Info( $"[Drop] Suppression de l'InventoryItem '{linked.Metadata?.ResourceName}' suite au drop" );
				linked.GameObject.Destroy();
			}

			RemoveWeapon( weapon );
		}
	}

	protected override void OnUpdate()
	{

		if ( !Player.IsLocallyControlled )
			return;

		// Block all weapon input while in a vehicle
		if ( Player.CurrentCar.IsValid() )
			return;

		// Block weapon input (slots + molette) pendant le placement/déplacement de prop
		if ( Player.Components.Get<OpenFramework.Systems.Tools.PropPlacer>( FindMode.EnabledInSelfAndDescendants )?.IsActive == true )
			return;

		// Block weapon input (slots + molette) pendant qu'on tient un objet via le grab system.
		// La molette sert a ajuster HoldDistance, Alt+souris sert a rotater l'objet.
		if ( Player.GameObject.Root != null && Player.GameObject.Root.Tags.Has( "is_grabbing" ) )
			return;

		// Block all weapon input (slots + mouse wheel) while the radial menu is open
		if ( OpenFramework.UI.QuickMenuSystem.PlayerRadialMenu.Instance != null
			&& OpenFramework.UI.QuickMenuSystem.PlayerRadialMenu.Instance.IsOpen )
			return;

		// Block weapon input pendant la saisie du code d'un coffre, sinon les
		// chiffres tapes pendant la fenetre de focus de la TextEntry switchent
		// l'arme (Slot1-5).
		if ( OpenFramework.UI.World.Storage.StorageCodePanel.Instance != null )
			return;

		// Block weapon input pendant qu'une UI ATM est ouverte pour le client local (scroll / Slot1-9)
		if ( Connection.Local != null )
		{
			if ( _atmCheckTimer > 0.1f )
			{
				_cachedAnyAtmOpen = false;
				foreach ( var atmUi in Scene.GetAllComponents<OpenFramework.AtmUI>() )
				{
					if ( atmUi.IsOpenForLocalClient ) { _cachedAnyAtmOpen = true; break; }
				}
				_atmCheckTimer = 0;
			}
			if ( _cachedAnyAtmOpen ) return;
		}

		// Block scroll + changement d'arme quand un terminal police est ouvert
		if ( PoliceComputer.IsAnyOpenLocally ) return;

		if ( Input.Pressed( "Drop" ) && Current.IsValid() )
		{
			Drop( Current );
			return;
		}

		// Empeche de ressortir une arme tant que le joueur a les mains en l'air
		if ( Player.IsHandsUp )
			return;

		foreach ( var slot in Enum.GetValues<EquipmentSlot>() )
		{
			if ( slot == EquipmentSlot.Undefined )
				continue;

			if ( !Input.Pressed( $"Slot{(int)slot}" ) )
				continue;

			SwitchToSlot( slot );
			return;
		}

		var wheel = Input.MouseWheel;
		if ( Input.Keyboard.Down( "ALT" ) ) return;

		// Handcuff
		if ( Player.IsHandcuffed )
		{
			wheel.y = 0;
		}
		// Handcuff

		if ( wheel.y == 0f ) return;

		var availableWeapons = Equipment.OrderBy( x => x.Resource.Slot ).ToList();
		if ( availableWeapons.Count == 0 )
			return;

		var currentSlot = 0;
		for ( var index = 0; index < availableWeapons.Count; index++ )
		{
			var weapon = availableWeapons[index];
			if ( !weapon.IsDeployed )
				continue;

			currentSlot = index;
			break;
		}

		var slotDelta = wheel.y > 0f ? 1 : -1;
		currentSlot += slotDelta;

		if ( currentSlot < 0 )
			currentSlot = availableWeapons.Count - 1;
		else if ( currentSlot >= availableWeapons.Count )
			currentSlot = 0;

		var weaponToSwitchTo = availableWeapons[currentSlot];
		if ( weaponToSwitchTo == Current )
			return;

		Switch( weaponToSwitchTo );
	}



	public void SwitchToBest()
	{
		if ( !Equipment.Any() )
			return;

		var priority = new[]
		{
			EquipmentSlot.Primary,
			EquipmentSlot.Secondary,
			EquipmentSlot.Melee,
			EquipmentSlot.Handheld,
			EquipmentSlot.Throwable,
		};

		foreach ( var slot in priority )
		{
			if ( HasInSlot( slot ) )
			{
				SwitchToSlot( slot );
				return;
			}
		}

		Switch( Equipment.FirstOrDefault() );
	}

	public void HolsterCurrent()
	{
		Assert.True( !IsProxy || Networking.IsHost );
		Player.SetCurrentEquipment( null );
	}

	public void SwitchToSlot( EquipmentSlot slot )
	{
		Assert.True( !IsProxy || Networking.IsHost );

		var equipment = Equipment
			.Where( x => x.Resource.Slot == slot )
			.ToArray();

		if ( equipment.Length == 0 )
			return;

		if ( equipment.Length == 1 && Current == equipment[0] && CanUnequipCurrentWeapon )
		{
			HolsterCurrent();
			return;
		}

		var index = Array.IndexOf( equipment, Current );
		Switch( equipment[(index + 1) % equipment.Length] );
	}

	/// <summary>
	/// Tries to set the player's current weapon to a specific one, which has to be in the player's inventory.
	/// </summary>
	/// <param name="equipment"></param>
	public void Switch( Equipment equipment )
	{
		Assert.True( !IsProxy || Networking.IsHost );

		if ( !Equipment.Contains( equipment ) )
			return;

		Player.SetCurrentEquipment( equipment );
	}

	/// <summary>
	/// Removes the given weapon and destroys it.
	/// </summary>
	public void RemoveWeapon( Equipment equipment )
	{
		Assert.True( Networking.IsHost );

		if ( !Equipment.Contains( equipment ) ) return;

		if ( Current == equipment )
		{
			var otherEquipment = Equipment.Where( x => x != equipment );
			var orderedBySlot = otherEquipment.OrderBy( x => x.Resource.Slot );
			var targetWeapon = orderedBySlot.FirstOrDefault();

			if ( targetWeapon.IsValid() )
			{
				Switch( targetWeapon );
			}
		}

		equipment.GameObject.Destroy();
		equipment.Enabled = false;
	}

	/// <summary>
	/// Removes the given weapon (by its resource data) and destroys it.
	/// </summary>
	public void Remove( EquipmentResource resource )
	{
		var equipment = Equipment.FirstOrDefault( w => w.Resource == resource );
		if ( !equipment.IsValid() ) return;
		RemoveWeapon( equipment );
	}

	public Equipment Give( EquipmentResource resource, bool makeActive = true )
	{
		Assert.True( Networking.IsHost );

		// If we're in charge, let's make some equipment.
		if ( resource == null )
		{
			return null;
		}

		var pickupResult = CanTake( resource );

		if ( pickupResult == PickupResult.None )
			return null;

		if ( pickupResult == PickupResult.Swap )
		{
			var slotCurrent = Equipment.FirstOrDefault( equipment => equipment.Enabled && equipment.Resource.Slot == resource.Slot );
			if ( slotCurrent.IsValid() )
				Drop( slotCurrent, true );
		}
		else if ( Has( resource ) )
		{
			return null;
		}

		if ( !resource.MainPrefab.IsValid() )
		{
			Log.Error( $"equipment doesn't have a prefab? {resource}, {resource.MainPrefab}, {resource.ViewModelPrefab}" );
			return null;
		}

		// Create the equipment prefab and put it on the GameObject.
		var gameObject = resource.MainPrefab.Clone( new CloneConfig()
		{
			Transform = new(),
			Parent = Player.GameObject
		} );
		var component = gameObject.GetComponentInChildren<Equipment>( true );
		component.Owner = Player;
		gameObject.NetworkSpawn( Player.Network.Owner );

		if ( makeActive )
			Player.SetCurrentEquipment( component );

		/*if ( component.Resource.Slot == EquipmentSlot.Special ) // C4 BOOM
		{
			Scene.Dispatch( new BombPickedUpEvent() );
		}*/

		return component;
	}

	public bool Has( EquipmentResource resource )
	{
		return Equipment.Any( weapon => weapon.Enabled && weapon.Resource == resource );
	}

	public bool HasInSlot( EquipmentSlot slot )
	{
		return Equipment.Any( weapon => weapon.Enabled && weapon.Resource.Slot == slot );
	}

	public enum PickupResult
	{
		None,
		Pickup,
		Swap
	}

	public PickupResult CanTake( EquipmentResource resource )
	{
		return !HasInSlot( resource.Slot ) ? PickupResult.Pickup : PickupResult.Swap;
	}
}
