using Facepunch;
using Sandbox.Events;
using OpenFramework.Api;
using OpenFramework.Extension;
using OpenFramework.Utility;

namespace OpenFramework.Systems.Jobs;

public enum JobList
{
	None,
	Citizen,
	Police,
	Armurier,
	Medic,
	Mayor,
	Maintenance,
	Eboueur,
	Cuisinier,
}

public record JoinJobEvent( Client client, string JobName ) : IGameEvent;
public record LeaveJobEvent( Client client, string JobName ) : IGameEvent;

// A attacher une fois dans ta scène (ex: sur un GameObject "GameServices")
public sealed class JobSystem : Component
{
	[Sync( SyncFlags.FromHost )]
	public List<JobComponent> Jobs { get; set; }

	public static JobSystem Instance { get; private set; }

	protected override void OnEnabled()
	{
		Instance = this;
	}

	protected override void OnDisabled()
	{
		if ( Instance == this )
			Instance = null;
	}

	protected override void OnAwake()
	{
		Jobs = GetComponents<JobComponent>().ToList();

		foreach(var job in Jobs)
		{
			/*Log.Info( "--------------------------------" );
			Log.Info( $"Identifier: {job.JobIdentifier}" );
			Log.Info( $"Name: {job.DisplayName}" );
			Log.Info( $"Color: {job.Color}" );
			Log.Info( $"Salary: {job.Salary}" );
			Log.Info( "--------------------------------" );*/
		}
	}

	[Rpc.Host]
	public static void SetJob( string jobname, string gradename = "" )
	{
		var callerClient = Rpc.Caller.GetClient();

		if ( callerClient == null )
		{
			Log.Error( "SetJob: Caller is not a valid client." );
			return;
		}

		SetJob( callerClient, jobname, gradename );
	}

	[Rpc.Host]
	public static async void SetJob( Client client, string jobname, string gradename = "" )
	{
		if ( client == null )
		{
			Log.Error( "SetJob: Caller is not a valid client." );
			return;
		}

		var job = GetJob( jobname );

		if ( job == null )
		{
			client.Notify( NotificationSystem.NotificationType.Error,
				"Ce métier n'existe pas ou n'est plus disponible." );
			return;
		}

		// Ancien job / grade pour les messages RP
		var previousJobName = GetJobName( client.Data.Job );
		var previousJobGrade = client.Data.JobGrade;
		var previousJobIdentifier = client.Data.Job;

		// Dispatch leave pour l'ancien job
		if ( !string.IsNullOrEmpty( previousJobIdentifier ) && previousJobIdentifier != jobname )
		{
			Game.ActiveScene.Dispatch( new LeaveJobEvent( client, previousJobIdentifier ) );
		}

		// Appliquer le job
		client.Data.Job = jobname;

		if ( jobname != "citizen" )
			client.JobSwitchEndTime = job.SwitchCooldown;

		client.Data.JobGrade = gradename;

		var previousJob = previousJobIdentifier;
		if ( previousJob == "citizen" && jobname != "citizen" )
		{
			if ( !string.IsNullOrEmpty( client.SavedClothingJson ) && client.SavedClothingJson != "[]" )
				client.SavedPersonalClothingJson = client.SavedClothingJson;
		}

		job.GiveClothing( client.PlayerPawn );

		// Event interne
		Game.ActiveScene.Dispatch( new JoinJobEvent( client, jobname ) );

		//----------------------------------------------------------
		// 🔔 Notifications RP
		//----------------------------------------------------------

		string gradeLabel = gradename;
		if ( !string.IsNullOrEmpty( gradename ) && job.HasGrades && job.Grades != null )
		{
			var gradeObj = job.Grades.FirstOrDefault( g => g.Name == gradename );
			if ( gradeObj != null )
				gradeLabel = gradeObj.Name;
		}

		string newJobText = string.IsNullOrEmpty( gradeLabel )
			? job.DisplayName
			: $"{gradeLabel} ({job.DisplayName})";

		if ( !string.IsNullOrEmpty( previousJobName ) && previousJobName != job.DisplayName )
		{
			if ( !string.IsNullOrEmpty( previousJobGrade ) )
			{
				//client.Notify( NotificationSystem.NotificationType.Info,$"Vous avez quitté votre poste de {previousJobGrade} ({previousJobName})." );
			}
			else
			{
				//client.Notify( NotificationSystem.NotificationType.Info,$"Vous avez quitté votre poste de {previousJobName}." );
			}
		}
		var character = await ApiComponent.Instance.GetActiveCharacter( client.SteamId );
		await ApiComponent.Instance.UpdadteActualJob( client.SteamId, character.Id, job.JobIdentifier );
		client.Notify( NotificationSystem.NotificationType.Success,
			$"Vous êtes maintenant {newJobText}." );

		if ( job.SwitchCooldown > 0f && jobname != "citizen" )
		{
			client.Notify( NotificationSystem.NotificationType.Info,
				$"Vous pourrez rechanger de métier dans {TimeUtils.DelayToReadable( job.SwitchCooldown )}." );
		}
	}

	[Rpc.Host]
	public static void LeaveJob()
	{
		var client = Rpc.Caller.GetClient();
		if ( client == null )
			return;

		var oldJobName = GetJobName( client.Data.Job );
		client.Data.Job = "citizen";


		client.Notify( NotificationSystem.NotificationType.Info,
				$"Vous avez quitté votre poste de {oldJobName}." );

		Game.ActiveScene.Dispatch( new LeaveJobEvent( client, oldJobName ) );

		// Récupérer l'ancien job
		/*var oldJobIdentifier = client.Data.Job;
		var oldJob = GetJob( oldJobIdentifier );
		var oldJobName = oldJob != null ? oldJob.DisplayName : oldJobIdentifier;
		var oldGrade = client.Data.JobGrade; // ← avant les modifications

		Log.Info( oldJob != null ? oldJob.DisplayName : oldJobIdentifier );

		// Trouver le job par défaut
		var defaultJob = Instance.Jobs.FirstOrDefault( x => x.IsDefault )
						  ?? Instance.Jobs.First();

		Log.Info( $"DefaultJob: {defaultJob}" );

		// Appliquer changement vers job par défaut
		client.Data.Job = defaultJob.JobIdentifier;
		client.Data.JobGrade = "";

		// ✅ Restaure les vêtements perso sauvegardés avant le job
		if ( client.PlayerPawn.IsValid()
			&& !string.IsNullOrEmpty( client.SavedPersonalClothingJson )
			&& client.SavedPersonalClothingJson != "[]" )
		{
			client.SavedClothingJson = client.SavedPersonalClothingJson;
			var paths = System.Text.Json.JsonSerializer.Deserialize<List<string>>( client.SavedPersonalClothingJson );
			if ( paths != null )
				Client.BroadcastEquipList( client.PlayerPawn.GameObject, paths, false );
		}

		// Dispatch event RP
		Game.ActiveScene.Dispatch( new LeaveJobEvent( client, oldJobName ) );

		// 🔔 Notifications RP
		if ( !string.IsNullOrEmpty( oldGrade ) )
		{
			client.Notify( NotificationSystem.NotificationType.Info,
				$"Vous avez quitté votre poste de {oldGrade} ({oldJobName})." );
		}
		else
		{
			client.Notify( NotificationSystem.NotificationType.Info,
				$"Vous avez quitté votre poste de {oldJobName}." );
		}*/
	}

	public static JobComponent GetJob( string job )
	{
		// Singleton.Instance evite un Scene.GetComponentInChildren<JobSystem>() (CollectAll)
		// qui scannait toute la scene a chaque appel — gros hot path identifie en profiler (~20% CPU)
		return Instance?.Jobs?.FirstOrDefault( x => x.JobIdentifier == job );
	}

	public static string GetJobName( string job )
	{
		return Instance?.Jobs?.FirstOrDefault( x => x.JobIdentifier == job )?.DisplayName;
	}
}
