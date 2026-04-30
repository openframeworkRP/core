namespace OpenFramework.Systems.Pawn;

partial class PlayerPawn
{
	private readonly List<Zone> _zones = new();

	// Throttle a 5Hz : Zone.GetAt scanne toutes les zones de la map et 50Hz est inutile
	// (UI affichage + UpdatePlayArea avec timer kill 5s tolerent largement 200ms de latence)
	private RealTimeUntil _nextZoneUpdate;

	/// <summary>
	/// Which <see cref="Zone"/>s is the player currently standing in.
	/// </summary>
	public IEnumerable<Zone> Zones => _zones;

	/// <summary>
	/// Update which <see cref="Zone"/>s the player is standing in.
	/// </summary>
	private void UpdateZones()
	{
		if ( !_nextZoneUpdate ) return;
		_nextZoneUpdate = 0.2f;

		_zones.Clear();
		_zones.AddRange( Zone.GetAt( WorldPosition ) );
	}

	public T GetZone<T>() where T : Component
	{
		return Zones.Select( x => x.GetComponent<T>() ).FirstOrDefault( x => x.IsValid() );
	}
}
