using Facepunch;
using OpenFramework.GameLoop;
using OpenFramework.Inventory;

namespace OpenFramework.Systems.Jobs;

/// <summary>
/// Gère le spawn périodique d'items "Déchet" dans les poubelles (TrashCan) de la map.
/// Les éboueurs doivent vider les poubelles et revendre les déchets au recycleur.
/// </summary>
public sealed class GarbageManager : Component
{
	/// <summary>
	/// Liste des items déchets pouvant apparaître dans les poubelles.
	/// Un item est choisi au hasard à chaque spawn.
	/// </summary>
	[Property, Group( "Settings" )]
	public List<ItemMetadata> PossibleTrashItems { get; set; } = new();

	/// <summary>
	/// Nombre min de déchets injectés par vague dans chaque poubelle choisie.
	/// </summary>
	[Property, Group( "Settings" )]
	public int MinTrashPerCan { get; set; } = 1;

	/// <summary>
	/// Nombre max de déchets injectés par vague dans chaque poubelle choisie.
	/// </summary>
	[Property, Group( "Settings" )]
	public int MaxTrashPerCan { get; set; } = 3;

	/// <summary>
	/// Nombre min de poubelles ciblées par vague.
	/// </summary>
	[Property, Group( "Settings" )]
	public int MinCansPerWave { get; set; } = 2;

	/// <summary>
	/// Nombre max de poubelles ciblées par vague.
	/// </summary>
	[Property, Group( "Settings" )]
	public int MaxCansPerWave { get; set; } = 5;

	/// <summary>
	/// Intervalle entre chaque vague de déchets (en secondes).
	/// </summary>
	[Property, Group( "Timing" )]
	public float WaveInterval { get; set; } = 120f;

	private TimeUntil NextWave { get; set; }

	protected override void OnAwake()
	{
		if ( !Networking.IsHost ) return;

		NextWave = WaveInterval;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;

		if ( !NextWave ) return;

		SpawnWave();
		NextWave = WaveInterval;
	}

	/// <summary>
	/// Vrai si au moins un joueur connecté est actuellement éboueur (job "maintenance").
	/// </summary>
	private static bool HasActiveEboueur()
	{
		var jobId = JobList.Maintenance.ToString();
		return GameUtils.AllPlayers
			.Any( c => string.Equals( c?.Data?.Job, jobId, StringComparison.OrdinalIgnoreCase ) );
	}

	private void SpawnWave()
	{
		if ( PossibleTrashItems.Count == 0 ) return;

		// Pas d'éboueur en service → on ne génère pas de déchets dans les poubelles.
		if ( !HasActiveEboueur() ) return;

		// Ne cibler que les poubelles qui ne sont pas pleines
		var availableCans = Scene.GetAllComponents<TrashBin>()
			.Where( x => !x.IsFull )
			.ToList();

		if ( availableCans.Count == 0 ) return;

		int cansToFill = Game.Random.Int( MinCansPerWave, Math.Min( MaxCansPerWave, availableCans.Count ) );

		var selected = availableCans.OrderBy( _ => Game.Random.Int( 0, 10000 ) ).Take( cansToFill );

		foreach ( var can in selected )
		{
			if ( can.Container == null ) continue;

			// Ne pas dépasser le MaxFillLevel de la poubelle
			int space = can.MaxFillLevel - can.FillLevel;
			int trashCount = Math.Min( Game.Random.Int( MinTrashPerCan, MaxTrashPerCan ), space );

			for ( int i = 0; i < trashCount; i++ )
			{
				var randomTrash = Game.Random.FromList( PossibleTrashItems );
				if ( randomTrash == null ) continue;

				InventoryContainer.Add( can.Container, randomTrash.ResourceName, 1 );
			}
		}
	}
}
