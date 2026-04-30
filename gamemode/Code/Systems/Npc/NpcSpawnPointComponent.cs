using Sandbox.Events;
using OpenFramework.GameLoop;
using OpenFramework.Systems.Jobs;

public class NpcSpawnPointComponent : Component, IGameEventHandler<JoinJobEvent>, IGameEventHandler<LeaveJobEvent>
{
	[Property] public GameObject Dealer { get; set; }
	[Property] public GameObject Reseller { get; set; }
	[Property] public GameObject Medic { get; set; }

	[Property] public float SafeRadius { get; set; } = 10f;

	private GameObject currentDealerPoint;
	private GameObject currentResellerPoint;
	private TimeUntil nextChange;

	public void OnGameEvent( JoinJobEvent e )
	{
		if ( !Networking.IsHost ) return;
		if ( e.JobName == "medic" ) UpdateMedicNpc();
		if ( e.JobName == "police" ) UpdateResellerNpc();
	}

	public void OnGameEvent( LeaveJobEvent e )
	{
		if ( !Networking.IsHost ) return;
		if ( e.JobName == "medic" ) UpdateMedicNpc( -1 );
		if ( e.JobName == "police" ) UpdateResellerNpc( -1 );
	}

	private void UpdateMedicNpc( int offset = 0 )
	{
		var job = JobSystem.GetJob( "medic" );
		if ( job == null ) return;

		if ( Medic != null )
			Medic.Enabled = (job.Employees.Count + offset) <= 0;
	}

	private void UpdateResellerNpc( int offset = 0 )
	{
		var job = JobSystem.GetJob( "police" );
		if ( job == null ) return;
		if ( Reseller == null ) return;

		Reseller.Enabled = (job.Employees.Count + offset) > 0;
	}

	private bool IsPlayerNearby( GameObject npc )
	{
		if ( npc == null ) return false;

		return Game.ActiveScene.GetComponents<PlayerPawn>()
			.Any( p => p.WorldPosition.Distance( npc.WorldPosition ) < SafeRadius );
	}

	protected override void OnStart()
	{
		if ( !Networking.IsHost ) return;

		Log.Info( $"[NpcSpawnPoint] Dealer: {Dealer?.Name ?? "NULL"}" );
		Log.Info( $"[NpcSpawnPoint] Medic: {Medic?.Name ?? "NULL"}" );
		Log.Info( $"[NpcSpawnPoint] Reseller: {Reseller?.Name ?? "NULL"}" );
		Log.Info( $"[NpcSpawnPoint] DealerPoints: {Constants.Instance.DealerSpawnPoints.Count}" );
		Log.Info( $"[NpcSpawnPoint] MedicPoint: {Constants.Instance.MedicSpawnPoint}" );
		/*
		if ( Medic != null )
		{
			var medicGo = Medic.Clone();
			medicGo.NetworkSpawn();
			Medic = medicGo;
		}*/

		if ( Dealer != null )
		{
			var dealerGo = Dealer.Clone();
			dealerGo.NetworkSpawn();
			Dealer = dealerGo;
		}

		if ( Reseller != null )
		{
			var resellerGo = Reseller.Clone();
			resellerGo.Enabled = false;
			resellerGo.NetworkSpawn();
			Reseller = resellerGo;

			Timer.Host( "ResellerSpawn", 0.5f, () =>
			{
				Reseller.Enabled = true;
				currentResellerPoint = TeleportToRandom( Reseller, Constants.Instance.ResellerSpawnPoints, currentResellerPoint );
				UpdateResellerNpc();
			} );
		}


		//currentMedicPoint = TeleportToPosition( Medic, Constants.Instance.MedicSpawnPoint );
		currentDealerPoint = TeleportToRandom( Dealer, Constants.Instance.DealerSpawnPoints, currentDealerPoint );
		currentResellerPoint = TeleportToRandom( Reseller, Constants.Instance.ResellerSpawnPoints, currentResellerPoint );
		nextChange = Constants.Instance.NpcChangeInterval;
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( !nextChange ) return;

		currentDealerPoint = TeleportToRandom( Dealer, Constants.Instance.DealerSpawnPoints, currentDealerPoint );
		currentResellerPoint = TeleportToRandom( Reseller, Constants.Instance.ResellerSpawnPoints, currentResellerPoint );
		nextChange = Constants.Instance.NpcChangeInterval;
	}

	/// <summary>
	/// Teleport Npc to random point use for in your case want to npc have a random position 
	/// </summary>
	/// <param name="npc"></param>
	/// <param name="points"></param>
	/// <param name="current"></param>
	/// <returns></returns>
	private GameObject TeleportToRandom( GameObject npc, List<GameObject> points, GameObject current )
	{
		if ( npc == null || points == null || points.Count == 0 ) return current;
		if ( IsPlayerNearby( npc ) ) return current;

		var available = points.Where( p => p != null && p.IsValid && p != current ).ToList();
		if ( available.Count == 0 ) available = points.Where( p => p != null && p.IsValid ).ToList();
		if ( available.Count == 0 ) return current;

		var next = Game.Random.FromList( available );
		if ( next == null || !next.IsValid ) return current;

		var groundPos = GetGroundPosition( next.WorldPosition );
		npc.WorldPosition = groundPos;
		npc.WorldRotation = next.WorldRotation;

		return next;
	}

	/// <summary>
	/// Teleport Npc to a single point 
	/// </summary>
	/// <param name="npc"></param>
	/// <param name="points"></param>
	/// <returns></returns>
	public GameObject TeleportToPosition( GameObject npc, GameObject points )
	{
		if ( npc == null || points == null ) return null;

		var groundPos = GetGroundPosition( points.WorldPosition );
		npc.WorldPosition = groundPos;
		npc.WorldRotation = points.WorldRotation;

		return npc;
	}


	/// <summary>
	/// Put npc to ground 
	/// </summary>
	/// <param name="from"></param>
	/// <returns></returns>
	private Vector3 GetGroundPosition( Vector3 from )
	{
		var trace = Scene.Trace
			.Ray( from + Vector3.Up * 50f, from + Vector3.Down * 500f )
			.WithoutTags( "npc", "player" )
			.Run();

		return trace.Hit ? trace.HitPosition : from;
	}
}
