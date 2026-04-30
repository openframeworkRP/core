namespace Sandbox.World;

public sealed class Atm : Component
{
	[Property, Sync(SyncFlags.FromHost)]
	public bool IsUsable { get; set; } = true;

	[Property, Sync( SyncFlags.FromHost )]
	public bool IsOffline { get; set; } = false;

	[Property, Sync(SyncFlags.FromHost)]
	public Client ClientWhoUse { get; set; }
	/*
	public UseResult CanUse( PlayerPawn player )
	{
		if(Constants.Instance.Debug)
			return true;

		var inventory = player.GetComponent<InventoryContainer>();

		if ( inventory.HasItem("card_bank") )
		{
			player.Client.Notify( NotificationType.Warning, "Veuillez patientez pendant le traitement." );
			return true;
		}
		else
		{
			player.Client.Notify( NotificationType.Warning, "Pas de carte bancaire" );
			return false;
		}
		return true;
	}
	
	public void OnUse( PlayerPawn player )
	{
		Log.Info( $"OnUse Called from server by: {player}" );

		if( IsOffline )
		{
			// Say to client that this ATM is non usable for the moment.
			player.Client.Notify( NotificationType.Warning, "Cet ATM est hors service pour le moment." );

			return;
		}
		
		if ( !IsUsable )
		{
			// Say to client that this ATM is non usable for the moment.
			player.Client.Notify( NotificationType.Warning, "Quelqu'un utilise déjà cet ATM, veuillez patienter, merci." );

			return;
		}
		

		if (IsUsable)
		{
			IsUsable = false;
			ClientWhoUse = player.Client;

			var panel = new AtmMenu();
			panel.ParentAtm = this;
			player.Client.AttachUI( panel );

			Log.Info( $"Client component Owner: {player.Network.Owner}" );
			using(Rpc.FilterInclude(player.Client.Connection))
			{

				Client.AttachATMMenu( this );
			}
		}
		else
		{
			// Say to client that this ATM is already being used by someone else.
		}
	}
		
	
	[Rpc.Host]
	public static void DisconnectFromATM(Atm atm)
	{
		atm.IsUsable = true;
		atm.ClientWhoUse = null;
	}

	public void OnGameEvent( OnNewDayEvent eventArgs )
	{
		Log.Warning( "ATM NE MARCHE PLUS" );
	}

	public void OnGameEvent( PlayerDisconnectedEvent eventArgs )
	{
		if(eventArgs.Player == ClientWhoUse)
		{
			IsUsable = true;
			ClientWhoUse = null;
		}
	}
	*/
}
