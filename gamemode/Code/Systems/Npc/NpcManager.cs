using Facepunch;
using System.Data;

namespace OpenFramework.Systems.Npc;

/// <summary>
/// A singleton component which handles the bots in a game. Gives them funny names.
/// </summary>
public sealed class NpcManager : SingletonComponent<NpcManager>
{
	/// <summary>
	/// The prefab to spawn when we want to make a player pawn for the player.
	/// </summary>
	[Property] public GameObject NpcPawnPrefab { get; set; }
	[Property, Sync(SyncFlags.FromHost)] public List<Npc> SpawnedNpc { get; set; }
	[Property] public List<Npc> Pedestrians => SpawnedNpc.Where( n => n.Role == Npc.NpcRole.Civilian ).ToList();

	[Rpc.Host]
	public static void SpawnNpc( SpawnPointInfo spawnPoint, GameObject parent = null, Npc.NpcRole role = Npc.NpcRole.Civilian )
	{
		SpawnNpc( spawnPoint.Position, spawnPoint.Rotation, parent, role );
	}

	[Rpc.Host]
	public static void SpawnNpc( Vector3 position, Rotation rotation, GameObject parent = null, Npc.NpcRole role = Npc.NpcRole.Civilian )
	{
		var obj = Instance.NpcPawnPrefab.Clone();
		var pawn = obj.GetComponent<PlayerPawn>();
		var npc = obj.GetComponent<Npc>();

		npc.Role = role;
		Instance.SpawnedNpc.Add( npc );
		pawn.IsNpc = true;
		pawn.WorldPosition = position;
		pawn.WorldRotation = rotation;
		pawn.SpawnPosition = position;
		pawn.SpawnRotation = rotation;

		if ( parent != null )
			obj.SetParent( parent );

		obj.Name = $"[NPC]";
		obj.NetworkSpawn( Connection.Host );

		/*var metadata = EquipmentResource.All.FirstOrDefault( x => x.ResourceName == "m4a1" );
		if ( metadata == null )
		{
			return;
		}

		pawn.Inventory.Give( metadata );*/
	}
}
