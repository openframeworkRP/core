using OpenFramework.GameLoop;
using OpenFramework.Systems.Weapons;
using OpenFramework.Utility;

namespace Facepunch;

public interface IWeighted
{
	float Weight { get; }
}

/// <summary>
/// A list of game utilities that'll help us achieve common goals with less code... I guess?
/// </summary>
public static partial class GameUtils
{
	/// <summary>
	/// All players in the game (includes disconnected players before expiration).
	/// </summary>
	public static IEnumerable<Client> AllPlayers => Game.ActiveScene.GetAllComponents<Client>();

	/// <summary>
	/// Get all players on a team.
	/// </summary>
	public static IEnumerable<Client> GetPlayers( ) => AllPlayers;

	/// <summary>
	/// Every <see cref="PlayerPawn"/> currently in the world.
	/// </summary>
	public static IEnumerable<PlayerPawn> PlayerPawns => AllPlayers.Select( x => x.PlayerPawn ).Where( x => x.IsValid() );

	public static IDescription GetDescription( GameObject go ) => go?.GetComponent<IDescription>();
	public static IDescription GetDescription( Component component ) => GetDescription( component?.GameObject );

	public static IEnumerable<SceneFile> GetAvailableMaps()
	{
		return ResourceLibrary.GetAll<SceneFile>().Where( x => x.GetMetadata( "IsVisibleInMenu", null ).ToBool() is true );
	}

	/// <summary>
	/// Get all spawn point transforms
	/// </summary>
	public static IEnumerable<SpawnPointInfo> GetSpawnPoints( params string[] tags ) => Constants.Instance
		.SpawnPlayers
		.Where( x => x != null && x.IsValid && x.GameObject.IsValid() )
		.Where( x => tags.Length == 0 || tags.Any( t => x.Tags?.Contains( t ) ?? false ) )
		.Select( x => new SpawnPointInfo( x.Transform.World, x.GameObject.Tags?.ToArray() ?? Array.Empty<string>() ) );

	/// <summary>
	/// Pick a random spawn point for the given team.
	/// </summary>
	public static SpawnPointInfo GetRandomSpawnPoint( params string[] tags )
	{
		var points = GetSpawnPoints( tags ).ToArray();

		// Fallback: si aucun spawn point dans Constants, chercher dans la scène
		if ( points.Length == 0 )
		{
			var scenePoints = Game.ActiveScene.GetAllComponents<SpawnPoint>()
				.Where( x => x.IsValid && x.GameObject.IsValid() )
				.Select( x => new SpawnPointInfo( x.Transform.World, x.GameObject.Tags?.ToArray() ?? Array.Empty<string>() ) )
				.ToArray();
			Log.Info( $"[Spawn] Fallback: {scenePoints.Length} SpawnPoints trouvés dans la scène" );
			if ( scenePoints.Length > 0 )
				return Random.Shared.FromArray( scenePoints, scenePoints[0] );
		}

		return Random.Shared.FromArray( points,
			new SpawnPointInfo( Transform.Zero, Array.Empty<string>() ) );
	}

	/// <summary>
	/// Get a player from a component that belongs to a player or their descendants.
	/// </summary>
	public static PlayerPawn GetPlayerFromComponent( Component component )
	{
		if ( component is PlayerPawn player ) return player;
		if ( !component.IsValid() ) return null;
		return !component.GameObject.IsValid() ? null : component.GameObject.Root.GetComponentInChildren<PlayerPawn>();
	}

	/// <summary>
	/// Get a player from a component that belongs to a player or their descendants.
	/// </summary>
	public static Pawn GetPawn( Component component )
	{
		if ( component is Pawn pawn ) return pawn;
		if ( !component.IsValid() ) return null;
		return !component.GameObject.IsValid() ? null : component.GameObject.Root.GetComponentInChildren<Pawn>();
	}

	public static Equipment FindEquipment( Component inflictor )
	{
		if ( inflictor is Equipment equipment )
		{
			return equipment;
		}

		return null;
	}

	/// <summary>
	/// Returns the invoking client to the main menu
	/// </summary>
	public static void ReturnToMainMenu()
	{
		var sc = ResourceLibrary.Get<SceneFile>( "scenes/menu.scene" );
		Game.ActiveScene.Load( sc );
	}
	public static T FromListWeighted<T>( this Random random, IReadOnlyList<T> list, T defaultValue = default )
		where T : IWeighted
	{
		if ( list.Count == 0 )
		{
			return defaultValue;
		}

		var totalWeight = list.Sum( x => x.Weight );

		if ( totalWeight <= 0f )
		{
			return defaultValue;
		}

		var value = random.NextSingle() * totalWeight;

		foreach ( var item in list )
		{
			if ( item.Weight < 0f )
			{
				throw new ArgumentException( "Weights must all be >= 0." );
			}

			value -= item.Weight;

			if ( value <= 0f )
			{
				return item;
			}
		}

		throw new Exception( "We should have returned an item already!" );
	}

	[Rpc.Broadcast]
	public static void PlaySoundFrom( string soundName, GameObject origin )
	{
		var resource = ResourceLibrary.Get<SoundEvent>( soundName );
		if ( resource == null ) return;

		var handle = Sound.Play( resource, origin.WorldPosition );
		if ( !handle.IsValid() ) return;
	}
}
