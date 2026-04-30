namespace OpenFramework.Systems.Npc;

public partial class Npc
{
	[Property, Group( "Traffic" ), FeatureEnabled("Traffic")] public bool UseTraffic { get; set; } = true;
	[Property, Group( "Traffic" ), Feature( "Traffic" )] public float SnapToWaypointMaxDist { get; set; } = 300f;

	//private TimeUntil _idleUntilTraffic;
}
