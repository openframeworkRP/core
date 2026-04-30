using Facepunch;

namespace OpenFramework.Extension;

public static class ConnectionExtension
{
	/// <summary>
	/// Retrieve the list of object owned by this connection.
	/// </summary>
	public static IEnumerable<GameObject> GetObjects( this Connection connection )
	{
		return Game.ActiveScene.GetAllObjects( true ).Where( x => x.Network.Owner == connection );
	}

	/// <summary>
	/// Retrieve the connection Pawn instance if available.
	/// </summary>
	public static PlayerPawn GetPawn( this Connection connection )
	{
		return GetClient( connection).PlayerPawn ?? null;
	}
	  
	/// <summary>
	/// Retrieve the connection Client instance if available.
	/// IMPORTANT : on filtre sur le parametre <paramref name="connection"/>, pas sur Rpc.Caller.
	/// Apres un await dans un Rpc.Host, Rpc.Caller n'est plus valide — capturer Rpc.Caller dans
	/// une variable locale avant l'await puis appeler .GetClient() dessus reste correct grace a ce
	/// filtrage par parametre.
	/// </summary>
	public static Client GetClient( this Connection connection )
	{
		if ( connection == null ) return null;
		return GameUtils.AllPlayers.FirstOrDefault( x => x.Connection == connection );
	}

	/// <summary>
	/// Retrieve the list of object owned by this connection.
	/// </summary>
	public static IEnumerable<GameObject> GetObject( this Connection connection )
	{
		return Game.ActiveScene.GetAllObjects( true ).Where(x => x.Network.Owner == connection);
	}

	/// <summary>
	/// Retrieve the list of object owned by this connection.
	/// </summary>
	/*public static void PushNotif(
	  this Connection connection,
	  BaseNotification.NotificationItem notification )
	{
		using ( Rpc.FilterInclude( connection ) )
		{
			GlobalGameNamespace.Log.Info( (object)"pushnotif" );
			BaseNotification.Create( notification );
		}
	}*/
}
