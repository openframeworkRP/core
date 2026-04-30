using Sandbox.Events;
using OpenFramework.Systems.Weapons;
using OpenFramework.Systems.Weapons.Grenades;

namespace OpenFramework.Systems.Weapons;

[Title( "Throw Weapon" ), Group( "Weapon Components" )]
public partial class Throwable : WeaponInputAction,
	IGameEventHandler<EquipmentHolsteredEvent>
{
	[Property, EquipmentResourceProperty] public float CookTime { get; set; } = 0.25f;
	[Property, EquipmentResourceProperty] public GameObject Prefab { get; set; }
	[Property] public float ThrowPower { get; set; } = 1200f;

	public enum State
	{
		Idle,
		Cook,
		Throwing,
		Thrown
	}

	[Sync] public State ThrowState { get; private set; }

	private TimeSince TimeSinceAction { get; set; }
	private bool HasThrownOnHost { get; set; }

	void IGameEventHandler<EquipmentHolsteredEvent>.OnGameEvent( EquipmentHolsteredEvent eventArgs )
	{
		if ( IsProxy ) return;
		if ( ThrowState == State.Thrown ) return;
		ThrowState = State.Idle;
	}

	protected bool CanThrow()
	{
		// Player
		if ( Equipment.Owner.IsFrozen )
			return false;

		return true;
	}

	protected override void OnInputDown()
	{
		if ( !CanThrow() )
			return;

		ThrowState = State.Cook;
		TimeSinceAction = 0;
	}

	protected override void OnInputUp()
	{
		if ( TimeSinceAction > CookTime && ThrowState == State.Cook )
		{
			ThrowState = State.Throwing;
			TimeSinceAction = 0;
		}
	}

	protected override void OnUpdate()
	{
		if ( Networking.IsHost && HasThrownOnHost && TimeSinceAction > 0.25f )
		{
			var player = Equipment.Owner;
			var linked = Equipment.LinkedItem;

			// Decremente la quantite de l'InventoryItem lie pour gerer le stack
			// de grenades. Si Quantity tombe a 0, OnQuantityChanged auto-detruit
			// l'item. Sans ca, RemoveWeapon ne supprimait que l'Equipment et
			// laissait l'item dans l'inventaire, permettant de lancer a l'infini.
			if ( linked != null && linked.IsValid )
				linked.Quantity -= 1;

			bool hasMoreInStack = linked != null && linked.IsValid && linked.Quantity > 0;

			HasThrownOnHost = false;

			if ( hasMoreInStack )
			{
				// Il reste des grenades dans le stack : on garde l'arme equipee
				// pour pouvoir lancer la suivante. Reset du state cote owner via RPC
				// (ThrowState est synced FromOwner, le host n'a pas l'autorite d'ecrire).
				ResetThrowStateForNext();
				TimeSinceAction = 0f;
				return;
			}

			// Stack vide : on retire l'arme du joueur.
			player.Inventory.RemoveWeapon( Equipment );
			return;
		}

		if ( IsProxy ) return;

		if ( Input.Pressed( "Attack2" ) )
		{
			ThrowState = State.Idle;
			TimeSinceAction = 0;
		}

		if ( !IsDown() && TimeSinceAction > CookTime && ThrowState == State.Cook )
		{
			ThrowState = State.Throwing;
			TimeSinceAction = 0;
			return;
		}

		if ( ThrowState == State.Throwing && TimeSinceAction > 0.15f )
		{
			Throw();
			ThrowState = State.Thrown;
			TimeSinceAction = 0f;
		}
	}

	[Rpc.Broadcast]
	protected void Throw()
	{
		var player = Equipment.Owner;

		if ( !IsProxy )
		{
			var tr = Scene.Trace.Ray( new( player.AimRay.Position, player.AimRay.Forward ), 10f )
				.IgnoreGameObjectHierarchy( GameObject.Root )
				.WithoutTags( "trigger" )
				.Run();

			var position = tr.Hit ? tr.HitPosition + tr.Normal * Equipment.Resource.WorldModel.Bounds.Size.Length : player.AimRay.Position + player.AimRay.Forward * 32f;
			position += player.EyeAngles.ToRotation().Right * 10f;

			var rotation = Rotation.From( 0, player.EyeAngles.yaw + 180f, 90f );
			var baseVelocity = player.CharacterController.Velocity;
			var dropped = Prefab.Clone( position, rotation );
			//dropped.Tags.Set( "no_player", true );

			var rb = dropped.GetComponentInChildren<Rigidbody>();
			rb.Velocity = baseVelocity + player.AimRay.Forward * ThrowPower + Vector3.Up * 100f;
			rb.AngularVelocity = Vector3.Random * 8.0f;

			var grenade = dropped.GetComponent<BaseGrenade>();
			if ( grenade.IsValid() )
				grenade.Player = player;

			dropped.Network.SetOrphanedMode( NetworkOrphaned.ClearOwner );
			dropped.NetworkSpawn();
		}

		if ( Equipment.Owner.IsValid() && Equipment.Owner.BodyRenderer.IsValid() )
		{
			Equipment.Owner.BodyRenderer.Set( "b_throw_grenade", true );
		}

		if ( !Networking.IsHost )
			return;

		TimeSinceAction = 0f;
		HasThrownOnHost = true;
	}

	[Rpc.Owner]
	private void ResetThrowStateForNext()
	{
		ThrowState = State.Idle;
		TimeSinceAction = 0f;
	}
}
