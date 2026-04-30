using Facepunch;
using Sandbox.Events;

/// <summary>
/// Called on the host when a new player joins, before NetworkSpawn is called.
/// </summary>
public record PlayerConnectedEvent( Client Client ) : IGameEvent;

/// <summary>
/// Called on the host when a new player joins, after NetworkSpawn is called.
/// </summary>
public record PlayerJoinedEvent( Client Player ) : IGameEvent;

/// <summary>
/// Called on the host when a client leaves
/// </summary>
public record PlayerDisconnectedEvent( Client Player ) : IGameEvent;

/// <summary>
/// Called on the host when a player (re)spawns.
/// </summary>
public record PlayerSpawnedEvent( PlayerPawn Player ) : IGameEvent;

/// <summary>
/// Called ON New Day
/// </summary>
public record OnNewDayEvent() : IGameEvent;
