using OpenFramework.Extension;
using OpenFramework.Systems.Dispatch;
using System.Linq;

namespace OpenFramework.Systems;

/// <summary>
/// Système de dispatch centralisé.
/// Toute la logique tourne sur le Host.
/// Les clients envoient des demandes via RPC.Host,
/// le Host broadcast les mises à jour via RPC.Broadcast.
/// </summary>
public class DispatchSystem : Component
{
	public static DispatchSystem Instance { get; private set; }

	// Liste des appels actifs (Pending + Accepted)
	// Non-synced : on pousse manuellement via RPC vers les clients concernés
	private readonly List<DispatchCall> _calls = new();
	private int _nextId = 1;

	protected override void OnAwake()
	{
		Instance = this;
	}

	// ─────────────────────────────────────────────
	//  ENVOI D'UN APPEL  (client → host)
	// ─────────────────────────────────────────────

	/// <summary>
	/// Appelé par n'importe quel client pour créer un appel de dispatch.
	/// </summary>
	[Rpc.Host]
	public static void SendCall( DispatchType type, string description, bool anonymous = false )
	{
		if ( !Networking.IsHost ) return;

		var caller   = Rpc.Caller.GetClient();
		var position = caller?.PlayerPawn?.WorldPosition ?? Vector3.Zero;
		var name     = anonymous ? "Anonyme" : (caller?.Data?.FirstName + " " + caller?.Data?.LastName).Trim();

		var call = new DispatchCall
		{
			Id          = Instance._nextId++,
			Type        = type,
			CallerName  = string.IsNullOrWhiteSpace( name ) ? "Anonyme" : name,
			Position    = position,
			Description = description,
			CreatedAt   = Time.Now,
			Status      = DispatchStatus.Pending,
		};

		Instance._calls.Add( call );

		Log.Info( $"[Dispatch] Nouvel appel #{call.Id} — {type.ToLabel()} par {call.CallerName}" );

		// Broadcast vers les destinataires
		BroadcastNewCall( call );
	}

	/// <summary>
	/// Crée un appel depuis le serveur (ex: via SMS dispatch, mort joueur).
	/// Appelé directement sur le Host sans passer par RPC.
	/// Retourne l'ID de l'appel créé, ou -1 si impossible.
	/// </summary>
	public static int SendCallFromServer( DispatchType type, string description, string callerName, Vector3 position )
	{
		if ( !Networking.IsHost || Instance == null ) return -1;

		var call = new DispatchCall
		{
			Id          = Instance._nextId++,
			Type        = type,
			CallerName  = string.IsNullOrWhiteSpace( callerName ) ? "Anonyme" : callerName,
			Position    = position,
			Description = description,
			CreatedAt   = Time.Now,
			Status      = DispatchStatus.Pending,
		};

		Instance._calls.Add( call );
		Log.Info( $"[Dispatch] Appel SMS #{call.Id} — {type.ToLabel()} par {call.CallerName}" );
		BroadcastNewCall( call );
		return call.Id;
	}

	/// <summary>
	/// Clôt un appel directement depuis le host (sans passer par un RPC),
	/// utile par exemple quand le joueur à l'origine de l'appel respawn / est ranimé.
	/// </summary>
	public static void CloseCallFromServer( int callId )
	{
		if ( !Networking.IsHost || Instance == null || callId < 0 ) return;

		var call = Instance._calls.FirstOrDefault( x => x.Id == callId );
		if ( call == null ) return;

		call.Status = DispatchStatus.Closed;
		Instance._calls.Remove( call );

		Log.Info( $"[Dispatch] Appel #{callId} clôturé automatiquement (serveur)" );
		BroadcastCallClosed( call.Id, call.Target );
	}

	/// <summary>
	/// Pousse le nouvel appel vers tous les clients. Le filtrage par job est fait côté client.
	/// </summary>
	private static void BroadcastNewCall( DispatchCall call )
	{
		DispatchUI.ReceiveNewCall(
			call.Id,
			(int)call.Type,
			call.CallerName,
			call.Position,
			call.Description,
			call.CreatedAt
		);
	}

	// ─────────────────────────────────────────────
	//  ACCEPTATION  (client → host)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void AcceptCall( int callId )
	{
		if ( !Networking.IsHost ) return;

		var call = Instance._calls.FirstOrDefault( x => x.Id == callId );
		if ( call == null || call.Status != DispatchStatus.Pending ) return;

		var responder = Rpc.Caller.GetClient();
		var name      = responder?.Data?.FirstName + " " + responder?.Data?.LastName;
		name = string.IsNullOrWhiteSpace( name.Trim() ) ? responder?.DisplayName ?? "Inconnu" : name.Trim();

		call.Status     = DispatchStatus.Accepted;
		call.AcceptedBy = name;

		Log.Info( $"[Dispatch] Appel #{callId} accepté par {name}" );

		// Broadcast la mise à jour + position révélée
		BroadcastCallAccepted( call );
	}

	private static void BroadcastCallAccepted( DispatchCall call )
	{
		DispatchUI.ReceiveCallAccepted( call.Id, call.AcceptedBy, call.Position );
	}

	// ─────────────────────────────────────────────
	//  CLÔTURE  (client → host)
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void CloseCall( int callId )
	{
		if ( !Networking.IsHost ) return;

		var call = Instance._calls.FirstOrDefault( x => x.Id == callId );
		if ( call == null ) return;

		call.Status = DispatchStatus.Closed;
		Instance._calls.Remove( call );

		Log.Info( $"[Dispatch] Appel #{callId} clôturé" );

		BroadcastCallClosed( call.Id, call.Target );
	}

	private static void BroadcastCallClosed( int callId, DispatchTarget target )
	{
		DispatchUI.ReceiveCallClosed( callId );
	}

	// ─────────────────────────────────────────────
	//  HELPER
	// ─────────────────────────────────────────────

	private static bool ShouldReceive( Connection connection, DispatchTarget target )
	{
		var job = connection.GetClient()?.Data?.Job?.ToLower();
		if ( job == null ) return false;

		bool isPolice = job == "police";
		bool isEms    = job == "medic";

		return target switch
		{
			DispatchTarget.Police => isPolice,
			DispatchTarget.EMS    => isEms,
			DispatchTarget.Both   => isPolice || isEms,
			_                     => false
		};
	}
}
