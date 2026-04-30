namespace OpenFramework.World.Devices.Apps.Phone;

public class PhoneMessage
{
	public string   SenderId   { get; set; }
	public string   SenderName { get; set; }
	public string   Content    { get; set; }
	public float    SentAt     { get; set; }
	public bool     IsMine     { get; set; }
	public MessageType Type    { get; set; } = MessageType.Text;

	public string TimeLabel
	{
		get
		{
			int secs = (int)(Time.Now - SentAt);
			if ( secs < 60 )  return "À l'instant";
			if ( secs < 3600 ) return $"Il y a {secs / 60}min";
			return $"Il y a {secs / 3600}h";
		}
	}
}

public enum MessageType
{
	Text,
	Dispatch, // Message spécial avec bouton dispatch
}

/// <summary>
/// Conversation entre le joueur local et un contact.
/// </summary>
public class PhoneConversation
{
	public string              ContactId   { get; set; }
	public string              ContactName { get; set; }
	public string              AvatarColor { get; set; }
	public List<PhoneMessage>  Messages    { get; set; } = new();

	public PhoneMessage LastMessage => Messages.LastOrDefault();

	public string LastPreview => LastMessage?.Type == MessageType.Dispatch
		? "🚨 Demande de dispatch"
		: LastMessage?.Content ?? "";
}
