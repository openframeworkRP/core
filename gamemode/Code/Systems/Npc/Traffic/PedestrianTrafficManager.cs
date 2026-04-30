namespace OpenFramework.Systems.Npc.Traffic;

public sealed class PedestrianTrafficManager : Component
{
	[Property] public List<GameObject> SpawnPoints { get; set; } = new();
	[Property] public int TargetPopulation { get; set; } = 25;

	private List<Npc> _spawned => NpcManager.Instance?.Pedestrians;
	private TimeSince _sinceAdjust;

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( _sinceAdjust < 1.0f ) return; // ajuste 1 fois / s
		if ( _spawned == null ) return;
		_sinceAdjust = 0f;

		// spawn
		while ( _spawned.Count < TargetPopulation )
		{
			var sp = PickSpawn();
			if ( sp is null ) break;

			NpcManager.SpawnNpc( sp.WorldPosition, sp.WorldRotation );
		}

		// cleanup (si certains ont été supprimés)
		_spawned.RemoveAll( n => !n.IsValid );
	}

	private GameObject PickSpawn()
	{
		var list = SpawnPoints?.Where( s => s.IsValid ).ToList();
		if ( list is { Count: > 0 } ) return Game.Random.FromList( list );

		// fallback: n’importe quel waypoint de la scène
		var any = Scene.GetAllComponents<SpawnPoint>().ToList();
		return any.Count > 0 ? Game.Random.FromList( any ).GameObject : null;
	}
}
