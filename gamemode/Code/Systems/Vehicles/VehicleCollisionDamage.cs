using OpenFramework.Systems.Pawn;
using DamageInfo = OpenFramework.Systems.Pawn.DamageInfo;

namespace OpenFramework.Systems.Vehicles;

/// <summary>
/// Inflicts damage to players hit by this vehicle based on collision speed.
/// Attach to the same GameObject as the Vehicle component.
/// Requires a Collider with CollisionEventsEnabled = true.
/// </summary>
public sealed class VehicleCollisionDamage : Component, Component.ICollisionListener
{
	[Property] public Vehicle Vehicle { get; set; }

	/// <summary>Minimum impact speed (km/h) to deal any damage.</summary>
	[Property] public float MinSpeedKmh { get; set; } = 15f;

	/// <summary>Damage multiplier applied to the impact speed (km/h).</summary>
	[Property] public float DamagePerKmh { get; set; } = 3f;

	/// <summary>Cooldown per player to avoid dealing damage every physics tick.</summary>
	private readonly Dictionary<PlayerPawn, TimeSince> _hitCooldowns = new();

	protected override void OnEnabled()
	{
		// Ensure collision events are enabled on the Rigidbody so ICollisionListener fires
		var rb = Components.GetInAncestorsOrSelf<Rigidbody>();
		if ( rb.IsValid() )
			rb.CollisionEventsEnabled = true;
	}

	public void OnCollisionStart( Collision collision )
	{
		if ( !Networking.IsHost ) return;

		var otherGo = collision.Other.GameObject;
		if ( otherGo == null ) return;

		// Find a PlayerPawn on the hit object or its ancestors
		var victim = otherGo.Components.GetInAncestorsOrSelf<PlayerPawn>();
		if ( victim == null || !victim.IsValid() ) return;

		// Don't damage passengers of this vehicle
		if ( victim.CurrentCar.IsValid() && victim.CurrentCar == Vehicle )
			return;

		// Don't damage dead players
		if ( victim.HealthComponent.State == LifeState.Dead )
			return;

		// Cooldown to prevent multi-hit in same collision
		if ( _hitCooldowns.TryGetValue( victim, out var timeSince ) && timeSince < 0.5f )
			return;
		_hitCooldowns[victim] = 0f;

		// Calculate impact speed in km/h
		float impactSpeed = collision.Contact.Speed.Length.InchToMeter() * 3.6f;

		if ( impactSpeed < MinSpeedKmh )
			return;

		float damage = impactSpeed * DamagePerKmh;

		// Find the driver as attacker (if any)
		Component attacker = null;
		if ( Vehicle.IsValid() )
		{
			var driverSeat = Vehicle.Components.GetAll<PlayerSeat>()
				.FirstOrDefault( s => s.HasInput && s.Player.IsValid() );
			if ( driverSeat != null )
				attacker = driverSeat.Player;
		}

		// Apply force in the vehicle's forward direction
		var force = Vehicle.IsValid() && Vehicle.Rigidbody.IsValid()
			? Vehicle.Rigidbody.Velocity.Normal * 500f + Vector3.Up * 200f
			: collision.Contact.Normal * 500f;

		victim.HealthComponent.TakeDamage( new DamageInfo(
			Attacker: attacker,
			Damage: damage,
			Position: collision.Contact.Point,
			Force: force
		) );
	}

	public void OnCollisionUpdate( Collision collision ) { }
	public void OnCollisionStop( CollisionStop other ) { }
}
