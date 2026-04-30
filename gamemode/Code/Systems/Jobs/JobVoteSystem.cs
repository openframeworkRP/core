using Facepunch;
using OpenFramework.Extension;

namespace OpenFramework.Systems.Jobs;

using Sandbox;
using Sandbox.Events;
using OpenFramework.ChatSystem;
using System;
using System.Linq;
using System.Text.Json.Serialization;

public struct JobVote
{
	public string TargetJob;
	public string TargetGrade;
	public string IssuerName;
	public ulong IssuerSteamId;
}

public sealed class JobVoteSystem : Component, IGameEventHandler<PlayerDisconnectedEvent>
{
	// --- Config ---
	[Property] private float VoteDelay { get; set; } = 35f; // secondes

	// --- État interne (serveur uniquement) ---
	public JobComponent PendingJob { get; private set; }
	public string PendingJobGrade { get; private set; }

	// ✅ référence complète serveur uniquement
	[JsonIgnore] public Client IssuerServer { get; private set; }

	[Sync( SyncFlags.FromHost )] public RealTimeUntil PendingDelay { get; private set; }
	[Sync( SyncFlags.FromHost )] public NetList<ulong> UpVotes { get; private set; } = new();
	[Sync( SyncFlags.FromHost )] public NetList<ulong> DownVotes { get; private set; } = new();

	public int UpVotesCount => UpVotes.Count;
	public int DownVotesCount => DownVotes.Count;

	// ✅ version client-safe (nom uniquement)
	[Sync( SyncFlags.FromHost )] public JobVote CurrentVote { get; private set; }

	public static JobVoteSystem Instance => Game.ActiveScene.GetComponentInChildren<JobVoteSystem>();

	private int TotalEligibleVoters => GameUtils.AllPlayers.Count( cl => cl != IssuerServer );

	// -------------------------------------------------------------
	// RPCs
	// -------------------------------------------------------------

	[Rpc.Host]
	public static void Throw( string jobId, string gradename = "" )
	{
		Log.Info( $"[VoteSystem] Throw() called with jobId='{jobId}', grade='{gradename}'" );

		if ( !Networking.IsHost )
		{
			Log.Info( "[VoteSystem] Aborted: Not running on host." );
			return;
		}

		var cl = Rpc.Caller.GetClient();
		if ( cl == null )
		{
			Log.Info( "[VoteSystem] Aborted: Rpc.Caller.GetClient() returned null." );
			return;
		}

		var sys = Instance;
		if ( sys is null || !sys.IsValid )
		{
			Log.Info( "[VoteSystem] Aborted: VoteSystem.Instance is null or invalid." );
			return;
		}

		if ( sys.PendingJob != null )
		{
			Log.Info( $"[VoteSystem] Aborted: A vote is already pending for job '{sys.PendingJob?.JobIdentifier}'." );
			return;
		}

		var job = JobSystem.GetJob( jobId );
		if ( job == null )
		{
			Log.Info( $"[VoteSystem] Aborted: No job found with ID '{jobId}'." );
			return;
		}

		if ( !job.WhitelistOnly )
		{
			Log.Info( $"[VoteSystem] Aborted: Job '{jobId}' is not WhitelistOnly (no vote needed)." );
			return;
		}

		Log.Info( $"[VoteSystem] Creating new vote for job '{job.JobIdentifier}' (grade: '{gradename}') by {cl.DisplayName}" );

		// 🔹 Prepare new vote
		sys.PendingJob = job;
		sys.PendingJobGrade = gradename;
		sys.PendingDelay = sys.VoteDelay;
		sys.IssuerServer = cl; // ✅ server-only
		job.IsBeingVoted = true;

		sys.UpVotes.Clear();
		sys.DownVotes.Clear();

		sys.CurrentVote = new JobVote
		{
			TargetJob = job.JobIdentifier,
			TargetGrade = gradename,
			IssuerName = cl.DisplayName,
			IssuerSteamId = cl.SteamId
		};

		Log.Info( $"[VoteSystem] Vote initialized. Broadcasting to clients..." );

		// 🔹 Notify clients (show UI)
		/*using ( Rpc.FilterExclude( Rpc.Caller ) )
			SendToClients( sys.CurrentVote );*/
		SendToClients( sys.CurrentVote );

		Log.Info( "[VoteSystem] Vote successfully sent to clients." );
	}


	[Rpc.Host]
	public static void AddUpVote()
	{
		if ( !Networking.IsHost ) return;
		var sys = Instance;
		if ( sys?.PendingJob == null ) return;

		var caller = Rpc.Caller.GetClient();
		var steamId = caller?.SteamId ?? 0UL;
		if ( steamId == 0 ) return;
		if ( caller == sys.IssuerServer ) return; // ne peut pas voter pour soi-même
		if ( sys.UpVotes.Contains( steamId ) || sys.DownVotes.Contains( steamId ) ) return;

		sys.UpVotes.Add( steamId );
	}

	[Rpc.Host]
	public static void AddDownVote()
	{
		if ( !Networking.IsHost ) return;
		var sys = Instance;
		if ( sys?.PendingJob == null ) return;

		var caller = Rpc.Caller.GetClient();
		var steamId = caller?.SteamId ?? 0UL;
		if ( steamId == 0 ) return;
		if ( caller == sys.IssuerServer ) return; // ne peut pas voter pour soi-même
		if ( sys.UpVotes.Contains( steamId ) || sys.DownVotes.Contains( steamId ) ) return;

		sys.DownVotes.Add( steamId );
	}

	// 🔹 diffuse vers tous les clients
	[Rpc.Broadcast]
	private static void SendToClients( JobVote vote )
	{
		// Le candidat ne reçoit pas l'UI de vote (il voit juste la notif progress)
		if ( Client.Local?.SteamId == vote.IssuerSteamId ) return;

		var rootpanel = Game.ActiveScene.GetComponentInChildren<PanelComponent>()?.Panel;
		if( rootpanel == null )
		{
			Log.Error( "JobVoteSystem: No root panel found to display JobVoteUI." );
			return;
		}
		rootpanel.AddChild(new JobVoteUI());
	}

	// -------------------------------------------------------------
	// Fin et logique serveur
	// -------------------------------------------------------------

	private void EndVote()
	{
		if ( !Networking.IsHost ) return;
		if ( PendingJob == null ) return;

		int ups = UpVotesCount;
		int downs = DownVotesCount;

		// Cas d'égalité : on ne change pas de job mais on applique quand même le cooldown
		if ( ups == downs )
		{
			// On calcule la fin du cooldown : Temps actuel + durée définie dans le job
			IssuerServer.JobSwitchEndTime = Time.Now + PendingJob.SwitchCooldown;
			Cleanup();
			return;
		}

		if ( ups > downs )
		{
			if ( IssuerServer != null )
				JobSystem.SetJob( IssuerServer, PendingJob.JobIdentifier, PendingJobGrade );
		}
		else
		{
			// Vote échoué
		}

		// Application du cooldown après le vote (réussi ou échoué)
		IssuerServer.JobSwitchEndTime = Time.Now + PendingJob.SwitchCooldown;
		Cleanup();
	}

	private void Cleanup()
	{
		if ( PendingJob != null )
			PendingJob.IsBeingVoted = false;

		PendingJob = null;
		PendingJobGrade = null;
		PendingDelay = 0;

		IssuerServer = null; // ✅ uniquement serveur
		UpVotes.Clear();
		DownVotes.Clear();
		CurrentVote = default;
		JobVoteUI.DeleteUI();
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( PendingJob == null ) return;

		var total = TotalEligibleVoters;
		var totalCast = UpVotesCount + DownVotesCount;

		bool timeOver = PendingDelay;
		bool everyoneVoted = total > 0 && totalCast >= total;

		if ( timeOver || everyoneVoted )
			EndVote();
	}

	public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
	{
		if ( eventArgs.Player == IssuerServer )
		{
			Cleanup();
			ChatUI.Receive( new ChatUI.ChatMessage()
			{
				//Background = "ui/background/jobs/mayor.png",
				Message = "Vote en cours annulé, le représentant s'est enfuie de la ville.",
				AuthorId = Connection.Host.Id,
			} );
		}
			
	}
}
