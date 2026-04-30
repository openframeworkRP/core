using Sandbox;
using System.Collections.Generic;

namespace OpenFramework.Systems;

public sealed class SpawnableService : Component
{
	[Property] public List<GameObject> Spawnables { get; set; } = new();
}

public static class Spawnable
{
	private static SpawnableService EnsureService()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return null;

		var svc = scene.GetComponentInChildren<SpawnableService>();
		if ( svc == null ) svc = scene.Components.Create<SpawnableService>();
		return svc;
	}

	/// <summary>
	/// Logique centrale de création unifiée (Core).
	/// </summary>
	private static GameObject InternalSpawn(
		string prefabPath,
		Vector3 position,
		Rotation rotation,
		Vector3 scale,
		Connection owner = null,
		bool networkSpawnNow = true,
		NetworkMode networkMode = NetworkMode.Object )
	{
		var service = EnsureService();
		if ( service == null ) return null;

		var prefab = GameObject.GetPrefab( prefabPath );
		if ( prefab == null ) return null;

		var obj = prefab.Clone();
		obj.WorldPosition = position;
		obj.WorldRotation = rotation;
		obj.WorldScale = scale;

		obj.NetworkMode = networkMode;
		obj.Network.Interpolation = true;

		if ( owner != null )
			obj.Network.AssignOwnership( owner );

		service.Spawnables.Add( obj );

		if ( networkSpawnNow && networkMode == NetworkMode.Object )
		{
			obj.NetworkSpawn();
		}

		return obj;
	}

	#region Host Methods

	/// <summary>
	/// Crée sur le Host et retourne l'objet SANS le diffuser (pour configurer le WorldItem).
	/// </summary>
	public static GameObject CreateWithReturnFromHost( string prefabPath, Transform transform, Connection owner = null )
	{
		if ( !Networking.IsHost ) return null;
		return InternalSpawn( prefabPath, transform.Position, transform.Rotation, transform.Scale, owner, false );
	}

	/// <summary>
	/// Spawn classique via le Serveur (répliqué partout) via Transform.
	/// </summary>
	[Rpc.Host]
	public static void Server( string prefabPath, Transform transform, Connection owner = null )
	{
		InternalSpawn( prefabPath, transform.Position, transform.Rotation, transform.Scale, owner, true );
	}

	/// <summary>
	/// Spawn classique via le Serveur (répliqué partout) via Position/Rotation.
	/// </summary>
	[Rpc.Host]
	public static void Server( string prefabPath, Vector3 position, Rotation rotation, Connection owner = null )
	{
		InternalSpawn( prefabPath, position, rotation, Vector3.One, owner, true );
	}

	#endregion

	#region Broadcast Methods

	/// <summary>
	/// Spawn local sur TOUS les clients via Transform.
	/// </summary>
	[Rpc.Broadcast]
	public static void Client( string prefabPath, Transform transform )
	{
		InternalSpawn( prefabPath, transform.Position, transform.Rotation, transform.Scale, null, false, NetworkMode.Never );
	}

	/// <summary>
	/// Spawn local sur TOUS les clients via Position/Rotation.
	/// </summary>
	[Rpc.Broadcast]
	public static void Client( string prefabPath, Vector3 position, Rotation rotation )
	{
		InternalSpawn( prefabPath, position, rotation, Vector3.One, null, false, NetworkMode.Never );
	}

	#endregion

	/// <summary>
	/// Destruction propre de l'objet et retrait de la liste du service.
	/// </summary>
	[Rpc.Host]
	public static void Destroy( GameObject obj )
	{
		if ( obj == null ) return;
		var service = EnsureService();
		service?.Spawnables.Remove( obj );
		obj.Destroy();
	}
}
