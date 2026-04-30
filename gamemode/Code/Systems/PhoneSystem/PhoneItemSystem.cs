using OpenFramework.Extension;
using OpenFramework.Inventory;
using OpenFramework.Systems;
using OpenFramework.Systems.Dispatch;
using OpenFramework.World.Devices.Apps.Messages;
using OpenFramework.World.Devices.Shared;

namespace OpenFramework.World.Devices;

public class PhoneItemSystem : Component
{
	// ── Propriétés éditeur ───────────────────────────────────────

	[Property] public ItemMetadata PhoneItemMetadata { get; set; }

	// ── État ─────────────────────────────────────────────────────

	public bool HasPhone => PhoneItem != null;
	public InventoryItem PhoneItem { get; private set; }

	// ── Historique notifications ──────────────────────────────────

	public record NotifEntry( string Title, string Icon );
	public List<NotifEntry> NotificationHistory { get; private set; } = new();
	private const int MaxHistory = 20;

	public void AddNotification( string title, string icon )
	{
		NotificationHistory.Add( new NotifEntry( title, icon ) );
		if ( NotificationHistory.Count > MaxHistory )
			NotificationHistory.RemoveAt( 0 );
	}

	public void ClearNotifications() => NotificationHistory.Clear();

	public static PhoneItemSystem Local =>
		Client.Local?.PlayerPawn?.Components.Get<PhoneItemSystem>( FindMode.EnabledInSelfAndDescendants );

	// ── Internals ────────────────────────────────────────────────

	private InventoryContainer _inventory;
	private RealTimeSince _lastCheck;
	private const float CheckInterval = 1f;

	protected override void OnStart()
	{
		_inventory = GameObject.Components.Get<InventoryContainer>( FindMode.EnabledInSelfAndDescendants );
		if ( _inventory == null )
			Log.Warning( "[PhoneItemSystem] Aucun InventoryContainer trouvé sur le pawn." );
	}

	protected override void OnUpdate()
	{
		if ( _lastCheck < CheckInterval ) return;
		_lastCheck = 0;
		RefreshPhoneItem();
	}

	public void Refresh() => RefreshPhoneItem();

	private void RefreshPhoneItem()
	{
		if ( _inventory == null ) { PhoneItem = null; return; }
		PhoneItem = _inventory.Items
			.FirstOrDefault( item => item?.Metadata != null && item.Metadata == PhoneItemMetadata );
	}

	// ─────────────────────────────────────────────
	//  HELPER
	// ─────────────────────────────────────────────

	private static bool ClientHasPhone( Client client )
	{
		if ( client?.PlayerPawn == null ) return false;
		var system = client.PlayerPawn.Components.Get<PhoneItemSystem>( FindMode.EnabledInSelfAndDescendants );
		if ( system == null ) { Log.Warning( $"[PhoneItemSystem] Introuvable sur {client.DisplayName}" ); return false; }
		return system.HasPhone;
	}

	// ─────────────────────────────────────────────
	//  ENVOI D'UN SMS
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void SendSms( string recipientSteamId, string content )
	{
		if ( !Networking.IsHost ) return;

		var sender = Rpc.Caller.GetClient();
		if ( sender == null ) return;

		if ( !ClientHasPhone( sender ) )
		{
			using ( Rpc.FilterInclude( sender.Connection ) )
				PushNotification( "Vous n'avez pas de téléphone.", "ui/icons/apps/messages_icon.svg" );
			return;
		}

		var senderName = $"{sender.Data?.FirstName} {sender.Data?.LastName}".Trim();
		if ( string.IsNullOrWhiteSpace( senderName ) ) senderName = sender.DisplayName;

		var recipient = Connection.All.FirstOrDefault( x => x.SteamId.ToString() == recipientSteamId );
		if ( recipient == null ) return;

		var recipientClient = recipient.GetClient();
		if ( !ClientHasPhone( recipientClient ) )
		{
			using ( Rpc.FilterInclude( sender.Connection ) )
				PushNotification( $"{recipientClient?.DisplayName ?? "Destinataire"} n'a pas de téléphone.", "ui/icons/apps/messages_icon.svg" );
			return;
		}

		using ( Rpc.FilterInclude( recipient ) )
		{
			MessagesApp.ReceiveSms( sender.SteamId.ToString(), senderName, content );
			var preview = content.Length > 40 ? content[..40] + "…" : content;
			PushNotification( $"{senderName} : {preview}", "ui/icons/apps/messages_icon.svg" );
		}

		using ( Rpc.FilterInclude( sender.Connection ) )
			MessagesApp.ConfirmSmsSent( recipientSteamId, content );

		Log.Info( $"[SMS] {senderName} → {recipient.Name} : {content}" );
	}

	// ─────────────────────────────────────────────
	//  ENVOI D'UNE DEMANDE DE DISPATCH
	// ─────────────────────────────────────────────

	[Rpc.Host]
	public static void SendDispatchRequest( string serviceId, string dispatchTypeStr, string description )
	{
		if ( !Networking.IsHost ) return;

		var sender = Rpc.Caller.GetClient();
		if ( sender == null ) return;

		if ( !ClientHasPhone( sender ) )
		{
			using ( Rpc.FilterInclude( sender.Connection ) )
				PushNotification( "Vous n'avez pas de téléphone.", "ui/icons/apps/messages_icon.svg" );
			return;
		}

		var senderName = $"{sender.Data?.FirstName} {sender.Data?.LastName}".Trim();
		if ( string.IsNullOrWhiteSpace( senderName ) ) senderName = sender.DisplayName;

		using ( Rpc.FilterInclude( sender.Connection ) )
			MessagesApp.ConfirmSmsSent( serviceId, $"🚨 Demande de dispatch : {dispatchTypeStr} — {description}" );

		if ( Enum.TryParse<DispatchType>( dispatchTypeStr, out var dispatchType ) )
			DispatchSystem.SendCallFromServer( dispatchType, description, senderName, sender.PlayerPawn?.WorldPosition ?? Vector3.Zero );

		Log.Info( $"[SMS Dispatch] {senderName} → Service {serviceId}" );
	}

	// ─────────────────────────────────────────────
	//  NOTIFICATION
	// ─────────────────────────────────────────────

	/// <summary>
	/// Push une notification vers l'écran ET la stocke dans l'historique.
	/// </summary>
	[Rpc.Broadcast]
	public static void PushNotification( string title, string icon )
	{
		Local?.AddNotification( title, icon );
		DeviceNotificationContainer.Push( title, icon );
		Sound.Play( "phone_notification" );
	}
}
