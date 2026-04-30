using Facepunch;
using Sandbox.Events;
using OpenFramework.ChatSystem;
using OpenFramework.UI;
using OpenFramework.UI.QuickMenuSystem;
using static Sandbox.Services.Inventory;

namespace OpenFramework.Systems.Jobs;

public class MayorJob : JobComponent, IGameEventHandler<KillEvent>
{
	public override string JobIdentifier => "mayor";

	[Sync( SyncFlags.FromHost )] public bool IsEmergencyActive { get; set; } = false;
	[Sync( SyncFlags.FromHost )] public NetList<string> Rules { get; set; } = new NetList<string>();

	[Property] SoundEvent AlertSound { get; set; }

	SoundHandle _emergencyHandle;

	public override List<MenuItem> PersonalActions()
	{
		base.PersonalActions();

		var list = new List<MenuItem>();

		if ( !IsEmergencyActive )
		{
			list.Add( new MenuItem( "Déclencher l'état d'urgence", () =>
			{
				ChatUI.Receive( new ChatUI.ChatMessage()
				{
					HostMessage = false,
					Message = "Le Maire de la ville a déclenché un état d'urgence",
					AuthorId = Connection.Host.Id,
					Processed = true,
				} );
				SetEmergency( true );
				EmergencySound();
			} ) );
		}
		else
		{
			list.Add( new MenuItem( "⚠ Stopper l'état d'urgence", () =>
			{
				ChatUI.Receive( new ChatUI.ChatMessage()
				{
					HostMessage = false,
					Message = "Le Maire a levé l'état d'urgence",
					AuthorId = Connection.Host.Id,
					Processed = true,
				} );
				SetEmergency( false );
				StopEmergencySound();
			} ) );
		}

		list.Add( new MenuItem( "Faire une annonce", () =>
		{
			AnnounceMayorMenu.Open();
		}, CloseMenuOnSelect: true ) );

		list.Add( new MenuItem( "Créer ou modifier des lois", () =>
		{
			RulesMayorMenu.Open();
		}, CloseMenuOnSelect: true ) );

		return list;
	}

	[Rpc.Host]
	public static void SetEmergency( bool state )
	{
		Game.ActiveScene.GetComponentInChildren<MayorJob>().IsEmergencyActive = state;
	}

	[Rpc.Broadcast]
	void EmergencySound()
	{
		_emergencyHandle = Sound.Play( AlertSound );
	}

	[Rpc.Broadcast]
	void StopEmergencySound()
	{
		_emergencyHandle.Stop();
	}

	[Rpc.Host]
	public static void AddRule( string value )
	{
		Game.ActiveScene.GetComponentInChildren<MayorJob>().Rules.Add( value );
	}

	[Rpc.Host]
	public static void DeleteRule( string value )
	{
		Game.ActiveScene.GetComponentInChildren<MayorJob>().Rules.Remove( value );
	}

	[Rpc.Host]
	public static void DeleteAllRules()
	{
		Game.ActiveScene.GetComponentInChildren<MayorJob>().Rules.Clear();
	}

	[Rpc.Host]
	public static void EditRule( string oldValue, string newValue )
	{
		var job = Game.ActiveScene.GetComponentInChildren<MayorJob>();
		var index = job.Rules.IndexOf( oldValue );
		if ( index < 0 ) return;
		job.Rules[index] = newValue;
	}

	public void OnGameEvent( KillEvent eventArgs )
	{
		if( !Networking.IsHost ) return;
		var player = GameUtils.GetPlayerFromComponent( eventArgs.DamageInfo.Victim );
		if ( !player.IsValid() ) return;

		var job = player.Client.Data.Job;

		if(job == "mayor")
		{
			DeleteAllRules();
			return;
		}
	}

}
