namespace OpenFramework.UI;

public partial class MarkerSystem : Panel
{
	public Dictionary<IMarkerObject, Marker> ActiveMarkers { get; set; } = new();

	private TimeSince _markerListTimer;
	private TimeSince _repositionTimer;
	private readonly List<IMarkerObject> _cachedMarkers = new();
	private readonly HashSet<IMarkerObject> _deleteSet = new();

	void Refresh()
	{
		if ( _markerListTimer > 0.5f )
		{
			_cachedMarkers.Clear();
			foreach ( var m in Scene.GetAllComponents<IMarkerObject>() )
				_cachedMarkers.Add( m );
			_markerListTimer = 0;
		}

		bool doReposition = _repositionTimer > 0.05f; // 20 fois/sec max
		if ( doReposition ) _repositionTimer = 0;

		_deleteSet.Clear();
		foreach ( var k in ActiveMarkers.Keys )
			_deleteSet.Add( k );

		foreach ( var markerObject in _cachedMarkers )
		{
			if ( UpdateMarker( markerObject, doReposition ) )
				_deleteSet.Remove( markerObject );
		}

		foreach ( var marker in _deleteSet )
		{
			ActiveMarkers[marker].Delete();
			ActiveMarkers.Remove( marker );
		}
	}

	public Marker CreateMarker( IMarkerObject marker )
	{
		var inst = new Marker()
		{
			Object = marker,
		};
		AddChild( inst );
		return inst;
	}

	public bool UpdateMarker( IMarkerObject marker, bool doReposition = true )
	{
		if ( !marker.GameObject.IsValid() )
			return false;

		if ( !marker.ShouldShow() )
			return false;

		var camera = Scene.Camera;
		if ( !camera.IsValid() )
			return false;

		if ( marker.MarkerMaxDistance != 0f && camera.WorldPosition.Distance( marker.MarkerPosition ) > marker.MarkerMaxDistance )
			return false;

		if ( !ActiveMarkers.TryGetValue( marker, out var instance ) )
		{
			instance = CreateMarker( marker );
			if ( instance.IsValid() )
				ActiveMarkers[marker] = instance;
		}

		if ( doReposition )
			instance.Reposition();

		return true;
	}

	public override void Tick()
	{
		Refresh();
	}
}
