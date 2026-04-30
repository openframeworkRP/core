namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Gère le spawn aléatoire de déchets sur la map pour le job agent d'entretien.
/// Utilise le NavMesh pour trouver des positions valides automatiquement.
/// </summary>
public sealed class MaintenanceTrashManager : Component
{
	/// <summary>
	/// Liste des prefabs de déchets communs (pondération uniforme).
	/// </summary>
	[Property] public List<GameObject> TrashPrefabs { get; set; } = new();

	/// <summary>
	/// Liste des prefabs de déchets rares (revente plus chère). Ne sont piochés qu'à <see cref="RareTrashChance"/>.
	/// </summary>
	[Property] public List<GameObject> RareTrashPrefabs { get; set; } = new();

	/// <summary>
	/// Probabilité (0..1) qu'un spawn pioche dans <see cref="RareTrashPrefabs"/> au lieu de <see cref="TrashPrefabs"/>.
	/// </summary>
	[Property, Range( 0f, 1f ), Step( 0.01f )] public float RareTrashChance { get; set; } = 0.05f;

	/// <summary>
	/// Nombre minimum de déchets par vague.
	/// </summary>
	[Property] public int MinPerWave { get; set; } = 3;

	/// <summary>
	/// Nombre maximum de déchets par vague.
	/// </summary>
	[Property] public int MaxPerWave { get; set; } = 8;

	/// <summary>
	/// Nombre maximum de déchets présents sur la map en même temps.
	/// </summary>
	[Property] public int MaxTrashOnMap { get; set; } = 20;

	/// <summary>
	/// Délai avant le spawn de la prochaine vague (en secondes).
	/// </summary>
	[Property] public float RespawnDelay { get; set; } = 30f;

	public List<GameObject> SpawnedTrash { get; set; } = new();

	private TimeUntil NextWaveDelay { get; set; }

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;

		NextWaveDelay = RespawnDelay;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		SpawnedTrash.RemoveAll( x => x == null || !x.IsValid() );

		if ( SpawnedTrash.Count > 0 ) return;

		if ( !NextWaveDelay ) return;

		SpawnWave();
		NextWaveDelay = RespawnDelay;
	}

	private void SpawnWave()
	{
		if ( TrashPrefabs.Count == 0 ) return;

		var spawnPoints = Scene.GetAllComponents<TrashSpawnPoint>().ToList();
		if ( spawnPoints.Count == 0 ) return;

		int min = Math.Min( MinPerWave, MaxPerWave );
		int max = Math.Max( MinPerWave, MaxPerWave );
		int count = Game.Random.Int( min, max );

		for ( int i = 0; i < count; i++ )
		{
			if ( SpawnedTrash.Count >= MaxTrashOnMap ) break;

			var spawnPoint = Game.Random.FromList( spawnPoints );
			if ( spawnPoint == null ) continue;

			var point = Scene.NavMesh.GetRandomPoint( spawnPoint.WorldPosition, spawnPoint.Radius );
			if ( !point.HasValue ) continue;

			var pickRare = RareTrashPrefabs.Count > 0 && Game.Random.Float() < RareTrashChance;
			var sourceList = pickRare ? RareTrashPrefabs : TrashPrefabs;
			var prefab = Game.Random.FromList( sourceList );
			if ( prefab == null ) continue;

			var obj = prefab.Clone( point.Value );
			obj.NetworkSpawn();
			SpawnedTrash.Add( obj );
		}
	}
}
