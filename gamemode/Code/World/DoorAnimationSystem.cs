using System;
using System.Collections.Generic;

namespace OpenFramework.World;

/// <summary>
/// Singleton local (par machine) qui centralise les ticks des portes (Door + RollingDoor).
///
/// Avant : chaque porte overridait OnFixedUpdate, donc le moteur dispatchait sur TOUTES
/// les portes de la map a 50Hz, sur host ET clients, meme quand la porte etait au repos.
/// Avec ~50-100 portes x 11 joueurs, ce dispatch dominait le profil
/// (Sandbox.Component.InternalFixedUpdate ~15% inclusive). Empiriquement, desactiver
/// les portes recuperait beaucoup de FPS.
///
/// Maintenant : une seule entree OnFixedUpdate pour le systeme. Les portes s'inscrivent
/// quand elles ont du travail a faire (animation en cours OU grace period en attente),
/// et se desinscrivent quand termine. Au repos = 0 dispatch dans tout le systeme.
///
/// Multi serveur dedie :
///  - Le systeme tourne sur HOST ET CLIENTS (NetworkMode.Never = local par machine).
///    L'animation est jouee localement par chaque client en lisant le State sync — pas
///    de sync de transform. Le ClearOwnership grace period n'agit que si IsHost (la
///    porte elle-meme garde le check `Networking.IsHost` dans TickGraceCheck).
///  - Le systeme est cree on-demand au premier RegisterX appel (premiere porte qui spawn).
///    Pas besoin de l'attacher manuellement a un prefab de scene.
///  - Pas de chemin d'item/argent touche : le refactor ne change que le lieu d'execution
///    des ticks, pas la logique d'ownership/transfer.
/// </summary>
public sealed class DoorAnimationSystem : Component
{
	public static DoorAnimationSystem Instance { get; private set; }

	private readonly HashSet<IDoorAnimated> _animating = new();
	private readonly HashSet<IDoorGraceable> _graceable = new();

	// Buffers reutilises pour iterer sans allouer chaque tick (et permettre la modification
	// des sets pendant l'iteration via auto-unregister depuis un Tick)
	private readonly List<IDoorAnimated> _animBuf = new();
	private readonly List<IDoorGraceable> _graceBuf = new();

	// Throttle 1Hz pour le grace check : la grace period dure 5 minutes,
	// 1 seconde de precision est largement suffisante.
	private RealTimeUntil _nextGraceCheck;

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;
	}

	public static void RegisterAnimator( IDoorAnimated d )
	{
		var sys = EnsureInstance();
		if ( sys != null ) sys._animating.Add( d );
	}

	public static void UnregisterAnimator( IDoorAnimated d )
	{
		if ( Instance.IsValid() ) Instance._animating.Remove( d );
	}

	public static void RegisterGraceable( IDoorGraceable d )
	{
		var sys = EnsureInstance();
		if ( sys != null ) sys._graceable.Add( d );
	}

	public static void UnregisterGraceable( IDoorGraceable d )
	{
		if ( Instance.IsValid() ) Instance._graceable.Remove( d );
	}

	private static DoorAnimationSystem EnsureInstance()
	{
		if ( Instance.IsValid() ) return Instance;

		var scene = Game.ActiveScene;
		if ( scene == null ) return null;

		// Local-only sur chaque machine : pas de replication reseau
		var go = scene.CreateObject();
		go.Name = "DoorAnimationSystem (auto)";
		go.NetworkMode = NetworkMode.Never;
		var sys = go.Components.Create<DoorAnimationSystem>();
		Instance = sys;
		return sys;
	}

	protected override void OnFixedUpdate()
	{
		// Animation : 50Hz, mais seulement sur les portes en cours d'animation
		if ( _animating.Count > 0 )
		{
			_animBuf.Clear();
			_animBuf.AddRange( _animating );
			for ( int i = 0; i < _animBuf.Count; i++ )
			{
				try
				{
					_animBuf[i].TickDoorAnimation();
				}
				catch ( Exception ex )
				{
					Log.Error( $"[DoorAnimationSystem] TickDoorAnimation: {ex}" );
				}
			}
		}

		// Grace period : 1Hz, host-only en pratique (verifie dans TickGraceCheck)
		if ( _graceable.Count > 0 && _nextGraceCheck )
		{
			_nextGraceCheck = 1f;
			_graceBuf.Clear();
			_graceBuf.AddRange( _graceable );
			for ( int i = 0; i < _graceBuf.Count; i++ )
			{
				try
				{
					_graceBuf[i].TickGraceCheck();
				}
				catch ( Exception ex )
				{
					Log.Error( $"[DoorAnimationSystem] TickGraceCheck: {ex}" );
				}
			}
		}
	}
}

/// <summary>Implemente par les composants qui ont une animation a faire avancer chaque tick.</summary>
public interface IDoorAnimated
{
	void TickDoorAnimation();
}

/// <summary>Implemente par les composants qui ont un grace period a surveiller.</summary>
public interface IDoorGraceable
{
	void TickGraceCheck();
}
