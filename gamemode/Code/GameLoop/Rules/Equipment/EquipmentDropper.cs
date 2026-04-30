using Sandbox.Events;
using OpenFramework.Systems.Weapons;

namespace Facepunch;

/// <summary>
/// Controls what equipment can be dropped by players, either when killed or with a key bind.
/// </summary>
public partial class EquipmentDropper : Component,
	IGameEventHandler<KillEvent>
{
	/// <summary>
	/// Which categories can we drop?
	/// </summary>
	[Property] public List<EquipmentSlot> Categories { get; set; } = new();

	/// <summary>
	/// If true, only drop at most one weapon and one item of utility on death,
	/// preferring most expensive. All special items are dropped, if allowed in
	/// <see cref="Categories"/>.
	/// </summary>
	[Property] public bool LimitedDropOnDeath { get; set; } = true;

	/// <summary>
	/// Can we drop this weapon?
	/// </summary>
	/// <param name="player"></param>
	/// <param name="weapon"></param>
	/// <returns></returns>
	public bool CanDrop( PlayerPawn player, Equipment weapon )
	{
		/*
		if ( weapon.Resource.Slot == EquipmentSlot.Punch ) 
			return false;*/

		if ( Categories.Count == 0 ) return true;

		return Categories.Contains( weapon.Resource.Slot );
	}

	void IGameEventHandler<KillEvent>.OnGameEvent( KillEvent eventArgs )
	{
		// Le drop a la mort est desactive : il a ete deplace dans
		// PlayerPawn.RespawnAtHospitalWithoutItems (timer expire sans EMS,
		// ou respawn manuel via F). Comme ca, un medic peut reanimer au defib
		// et le joueur garde toutes ses armes equipees.
	}

	/// <summary>
	/// Drop les armes equipees du joueur au sol (ancien comportement OnKill).
	/// Appele depuis RespawnAtHospitalWithoutItems quand le joueur perd
	/// definitivement son inventaire.
	/// </summary>
	public void DropEquipmentForPlayer( PlayerPawn player )
	{
		if ( !Networking.IsHost ) return;
		if ( !player.IsValid() ) return;

		// On recupere tout ce qui n'est PAS du slot Punch ni les "armes" speciales
		// "hand"/"punch" (poings invisibles, slot Primary mais pas une vraie arme).
		var realItems = player.Inventory.Equipment
			.Where( x => x.Resource.Slot != EquipmentSlot.Punch
				&& x.Resource.Name != "hand"
				&& x.Resource.Name != "punch" )
			.ToList();

		if ( LimitedDropOnDeath )
		{
			var melee = realItems.FirstOrDefault( x => x.Resource.Slot == EquipmentSlot.Melee );
			var gun = realItems.FirstOrDefault( x => x.Resource.Slot == EquipmentSlot.Primary || x.Resource.Slot == EquipmentSlot.Secondary );

			if ( gun.IsValid() ) player.Inventory.Drop( gun );
			if ( melee.IsValid() ) player.Inventory.Drop( melee, true );
		}
		else
		{
			foreach ( var eq in realItems )
			{
				player.Inventory.Drop( eq, true );
			}
		}

		player.Inventory.Clear();
	}


}
