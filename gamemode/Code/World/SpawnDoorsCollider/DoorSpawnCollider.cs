using Sandbox;
using OpenFramework.Command;
using CharacterController = OpenFramework.Systems.Pawn.CharacterController;

namespace OpenFramework;

public sealed class DoorSpawnCollider : Component, Component.ITriggerListener
{
	[Property] public BoxCollider WallCollider { get; set; } // le BoxCollider que tu as créé

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var player = other.GameObject.Components.GetInAncestors<PlayerPawn>();
		if ( !player.IsValid() || player.IsProxy ) return;

		player.IsPassingDoor = true;
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var player = other.GameObject.Components.GetInAncestors<PlayerPawn>();
		if ( !player.IsValid() || player.IsProxy ) return;

		player.IsPassingDoor = false;
		GameObject.Enabled = false;
	}
}
