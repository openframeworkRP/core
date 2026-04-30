using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace OpenFramework.GameLoop;

/// <summary>
/// Frame-driven timer service. Supports:
/// - Game-time timers (TimeUntil) w/ pause/resume and repeat
/// - Driftless repeats (keeps cadence stable over time)
/// - Optional real-time timers (RealTimeUntil), separate list
/// - Duplicate name protection + query helpers
/// - Client action registry for RPC-safe client timers
/// </summary>
public sealed class TimerService : Component
{
	// ---------- GAME-TIME TIMER ----------
	internal record TimerData( string uniqueName, float delay, Action action, bool repeat = false, bool driftless = true )
	{
		// Remaining time (game-time)
		internal TimeUntil _cd = delay;

		// For driftless cadence (anchor + tick count)
		internal float _anchorTime = Time.Now;
		internal int _fires = 0;

		internal bool _paused = false;
		internal float _pausedRemaining = 0f;

		internal Action _ended;

		internal void Tick()
		{
			if ( _paused )
				return;

			if ( _cd )
			{
				try
				{
					action?.Invoke();
				}
				catch ( Exception e )
				{
					Log.Error( $"[Timer] '{uniqueName}' threw: {e}" );
				}

				if ( repeat )
				{
					if ( driftless )
					{
						// Schedule next fire based on initial anchor to avoid drift.
						_fires++;
						var nextTarget = _anchorTime + (_fires + 1) * delay;
						var remaining = MathF.Max( 0.0001f, nextTarget - Time.Now );
						_cd = remaining;
					}
					else
					{
						_cd = delay; // classic behavior
					}
				}
				else
				{
					_ended?.Invoke();
				}
			}
		}
	}

	// ---------- REAL-TIME TIMER ----------
	internal record RealTimerData( string uniqueName, float delay, Action action, bool repeat = false, bool driftless = true )
	{
		// Remaining time (real-time)
		internal RealTimeUntil _cd = delay;

		// For driftless cadence
		internal double _anchorTime = RealTime.GlobalNow;
		internal int _fires = 0;

		internal bool _paused = false;
		internal float _pausedRemaining = 0f; // seconds remaining when paused

		internal Action _ended;

		internal void Tick()
		{
			if ( _paused )
				return;

			if ( _cd )
			{
				try
				{
					action?.Invoke();
				}
				catch ( Exception e )
				{
					Log.Error( $"[Timer] '{uniqueName}' (real) threw: {e}" );
				}

				if ( repeat )
				{
					if ( driftless )
					{
						_fires++;
						var nextTarget = _anchorTime + (_fires + 1) * delay;
						var remaining = (float)Math.Max( 0.0001, nextTarget - RealTime.GlobalNow );
						_cd = remaining;
					}
					else
					{
						_cd = delay;
					}
				}
				else
				{
					_ended?.Invoke();
				}
			}
		}
	}

	// Store timers
	[Property] internal List<TimerData> _timers = new();
	[Property] internal List<RealTimerData> _realTimers = new();

	// -------- Core start (game-time) --------
	internal void Start( TimerData timer )
	{
		// Replace any existing timer with same name (keeps API forgiving)
		var existing = _timers.FirstOrDefault( t => t.uniqueName == timer.uniqueName );
		if ( existing != null )
			_timers.Remove( existing );

		timer._ended = () => _timers.Remove( timer );
		timer._anchorTime = Time.Now;
		timer._cd = timer.delay; // ensure fresh remaining
		_timers.Add( timer );
	}

	// -------- Core start (real-time) --------
	internal void Start( RealTimerData timer )
	{
		var existing = _realTimers.FirstOrDefault( t => t.uniqueName == timer.uniqueName );
		if ( existing != null )
			_realTimers.Remove( existing );

		timer._ended = () => _realTimers.Remove( timer );
		timer._anchorTime = RealTime.GlobalNow;
		timer._cd = timer.delay;
		_realTimers.Add( timer );
	}

	// --- Helpers / Queries (game-time) ---
	internal bool Exists( string uniqueName ) => _timers.Any( x => x.uniqueName == uniqueName );
	internal bool ExistsReal( string uniqueName ) => _realTimers.Any( x => x.uniqueName == uniqueName );

	internal float Remaining( string uniqueName )
	{
		var t = _timers.FirstOrDefault( x => x.uniqueName == uniqueName );
		return t is null ? 0f : t._cd; // implicit -> seconds remaining
	}

	internal float RemainingReal( string uniqueName )
	{
		var t = _realTimers.FirstOrDefault( x => x.uniqueName == uniqueName );
		return t is null ? 0f : t._cd; // implicit -> seconds remaining
	}

	internal bool IsPaused( string uniqueName )
		=> _timers.FirstOrDefault( x => x.uniqueName == uniqueName )?._paused ?? false;

	internal bool IsPausedReal( string uniqueName )
		=> _realTimers.FirstOrDefault( x => x.uniqueName == uniqueName )?._paused ?? false;

	// --- Pause / Resume / Cancel (return bools for control flow) ---
	internal bool Pause( string uniqueName )
	{
		var t = _timers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null || t._paused ) return false;
		t._pausedRemaining = t._cd;
		t._paused = true;
		return true;
	}

	internal bool Resume( string uniqueName )
	{
		var t = _timers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null || !t._paused ) return false;
		t._cd = MathF.Max( 0.0001f, t._pausedRemaining );
		t._paused = false;
		return true;
	}

	internal bool Cancel( string uniqueName )
	{
		var t = _timers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null ) return false;
		_timers.Remove( t );
		return true;
	}

	internal bool PauseReal( string uniqueName )
	{
		var t = _realTimers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null || t._paused ) return false;
		t._pausedRemaining = t._cd;
		t._paused = true;
		return true;
	}

	internal bool ResumeReal( string uniqueName )
	{
		var t = _realTimers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null || !t._paused ) return false;
		t._cd = MathF.Max( 0.0001f, t._pausedRemaining );
		t._paused = false;
		return true;
	}

	internal bool CancelReal( string uniqueName )
	{
		var t = _realTimers.FirstOrDefault( x => x.uniqueName == uniqueName );
		if ( t is null ) return false;
		_realTimers.Remove( t );
		return true;
	}

	protected override void OnUpdate()
	{
		if ( _timers.Count > 0 )
			for ( int i = _timers.Count - 1; i >= 0; --i ) _timers[i].Tick();

		if ( _realTimers.Count > 0 )
			for ( int i = _realTimers.Count - 1; i >= 0; --i ) _realTimers[i].Tick();
	}

	protected override void OnDestroy()
	{
		_timers.Clear();
		_realTimers.Clear();
		base.OnDestroy();
	}
}

public static class Timer
{
	// -------- Ensure service in active scene --------
	private static TimerService EnsureService()
	{
		var scene = Game.ActiveScene;
		if ( scene == null )
		{
			Log.Error( "[Timer] No active scene available." );
			return null;
		}

		var svc = scene.GetComponentInChildren<TimerService>();
		if ( svc == null )
			svc = scene.Components.Create<TimerService>();

		return svc;
	}

	// ======================== HOST (GAME-TIME) ========================
	// Backward compatible
	public static void Host( string uniqueName, float delay, Action action, bool repeat = false )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[Timer] Host(...) called on client. Call a [Rpc.Host] method that invokes Host(...) on the server." );
			return;
		}

		var svc = EnsureService();
		if ( svc == null ) return;

		svc.Start( new TimerService.TimerData( uniqueName, delay, action, repeat, driftless: true ) );
	}

	// Quality-of-life sugar
	public static void HostEvery( string uniqueName, float interval, Action action ) => Host( uniqueName, interval, action, repeat: true );
	public static void HostAfter( string uniqueName, float delay, Action action ) => Host( uniqueName, delay, action, repeat: false );
	public static void HostNext( string uniqueName, Action action ) => Host( uniqueName, 0.0001f, action, repeat: false );

	[Rpc.Host]
	public static void HostPause( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.Pause( uniqueName ) )
			Log.Warning( $"[Timer] HostPause: '{uniqueName}' not found or already paused." );
	}

	[Rpc.Host]
	public static void HostResume( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.Resume( uniqueName ) )
			Log.Warning( $"[Timer] HostResume: '{uniqueName}' not found or not paused." );
	}

	[Rpc.Host]
	public static void HostCancel( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.Cancel( uniqueName ) )
			Log.Warning( $"[Timer] HostCancel: '{uniqueName}' not found." );
	}

	// ======================== HOST (REAL-TIME) ========================
	public static void HostReal( string uniqueName, float delay, Action action, bool repeat = false )
	{
		if ( !Networking.IsHost )
		{
			Log.Warning( "[Timer] HostReal(...) called on client." );
			return;
		}

		var svc = EnsureService();
		if ( svc == null ) return;

		svc.Start( new TimerService.RealTimerData( uniqueName, delay, action, repeat, driftless: true ) );
	}

	public static void HostRealEvery( string uniqueName, float interval, Action action ) => HostReal( uniqueName, interval, action, repeat: true );
	public static void HostRealAfter( string uniqueName, float delay, Action action ) => HostReal( uniqueName, delay, action, repeat: false );

	[Rpc.Host]
	public static void HostRealPause( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.PauseReal( uniqueName ) )
			Log.Warning( $"[Timer] HostRealPause: '{uniqueName}' not found or already paused." );
	}

	[Rpc.Host]
	public static void HostRealResume( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.ResumeReal( uniqueName ) )
			Log.Warning( $"[Timer] HostRealResume: '{uniqueName}' not found or not paused." );
	}

	[Rpc.Host]
	public static void HostRealCancel( string uniqueName )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !svc.CancelReal( uniqueName ) )
			Log.Warning( $"[Timer] HostRealCancel: '{uniqueName}' not found." );
	}

	// ======================== CLIENT SIDE ========================
	// NOTE: Delegates don't serialize. This keeps your existing signature working
	// by scheduling the delegate LOCALLY on each client that runs this method.
	[Rpc.Broadcast]
	public static void Client( string uniqueName, float delay, Action action, bool repeat = false )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		svc.Start( new TimerService.TimerData( uniqueName, delay, action, repeat, driftless: true ) );
	}

	// Registry-based client scheduling (RPC-safe)
	private static readonly Dictionary<string, Action> ClientActionRegistry = new();

	/// <summary> Register a client-only action bound to a key. </summary>
	public static void ClientRegister( string key, Action action )
	{
		ClientActionRegistry[key] = action;
	}

	/// <summary> Schedule by key across clients. Safe over RPC. </summary>
	[Rpc.Broadcast]
	public static void ClientSchedule( string uniqueName, float delay, string actionKey, bool repeat = false )
	{
		var svc = EnsureService();
		if ( svc == null ) return;

		if ( !ClientActionRegistry.TryGetValue( actionKey, out var act ) )
		{
			Log.Warning( $"[Timer] No client action for key '{actionKey}'." );
			return;
		}

		svc.Start( new TimerService.TimerData( uniqueName, delay, act, repeat, driftless: true ) );
	}

	[Rpc.Broadcast] public static void ClientPause( string uniqueName ) { EnsureService()?.Pause( uniqueName ); }
	[Rpc.Broadcast] public static void ClientResume( string uniqueName ) { EnsureService()?.Resume( uniqueName ); }
	[Rpc.Broadcast] public static void ClientCancel( string uniqueName ) { EnsureService()?.Cancel( uniqueName ); }

	// ======================== QUICK QUERIES ========================
	public static float HostRemaining( string uniqueName ) => EnsureService()?.Remaining( uniqueName ) ?? 0f;
	public static float HostRemainingReal( string uniqueName ) => EnsureService()?.RemainingReal( uniqueName ) ?? 0f;
	public static bool HostExists( string uniqueName ) => EnsureService()?.Exists( uniqueName ) ?? false;
	public static bool HostExistsReal( string uniqueName ) => EnsureService()?.ExistsReal( uniqueName ) ?? false;
}
