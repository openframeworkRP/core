namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Zone de spawn de déchets à placer sur les trottoirs.
/// Le MaintenanceTrashManager choisira un point aléatoire sur le NavMesh dans le rayon de cette zone.
/// </summary>
public sealed class TrashSpawnPoint : Component
{
	/// <summary>
	/// Rayon de la zone de spawn autour de ce point.
	/// </summary>
	[Property] public float Radius { get; set; } = 50f;

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = new Color( 0f, 1f, 0.3f, 0.3f );
		Gizmo.Draw.SolidCylinder( Vector3.Zero, Vector3.Up * 10f, Radius, 32 );

		Gizmo.Draw.Color = new Color( 0f, 1f, 0.3f, 0.8f );
		Gizmo.Draw.LineCylinder( Vector3.Zero, Vector3.Up * 10f, Radius, Radius, 32 );
	}
}
