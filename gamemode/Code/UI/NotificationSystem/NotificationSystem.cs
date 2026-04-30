using Facepunch;
using System.Collections.Generic;
using System.Linq;

namespace Facepunch;

public sealed class NotificationSystem : Component
{
	public enum NotificationType
	{
		Generic = 0,
		Error,
		Info,
		Warning,
		Success
	}

	/// <summary>
	/// File d'attente des notifications en attente d'affichage.
	/// Une seule notification est visible a la fois ; les suivantes patientent ici.
	/// Le NotificationContainer depile au fur et a mesure dans son Tick.
	/// </summary>
	public static readonly LinkedList<(NotificationType Type, string Message)> PendingQueue = new();

	/// <summary>
	/// Nombre maximum de notifs qu'on garde en attente. Si la queue est deja pleine,
	/// les nouvelles notifs sont ignorees (pour eviter la saturation en cas de spam).
	/// </summary>
	public const int MAX_QUEUE_SIZE = 20;

	[Rpc.Broadcast]
	public static void Notify(NotificationType type, string message)
	{
		NotifyLocal( type, message );
	}

	/// <summary>
	/// Affiche une notification UNIQUEMENT sur le client courant, sans broadcast réseau.
	/// À utiliser quand on est déjà à l'intérieur d'un autre [Rpc.Broadcast]
	/// (sinon Notify déclencherait un second broadcast vers tous les clients).
	/// </summary>
	public static void NotifyLocal( NotificationType type, string message )
	{
		var container = NotificationContainer.Instance;
		if ( container == null )
		{
			Log.Warning( "[NotificationSystem] NotifyLocal appelee mais NotificationContainer.Instance == null" );
			return;
		}

		// Cherche une notif identique deja visible : on stack le compteur sans
		// la remettre en queue (evite le spam visuel de la meme action repetee)
		var existing = container.Children
			.OfType<NotificationItem>()
			.FirstOrDefault(n => n.Matches(type, message));
		if (existing != null)
		{
			existing.Stack();
			return;
		}

		// Si aucune notif visible actuellement, affiche directement
		if (!container.Children.OfType<NotificationItem>().Any())
		{
			container.AddChild(new NotificationItem(type, message));
			return;
		}

		// Sinon, met en file d'attente
		if (PendingQueue.Count < MAX_QUEUE_SIZE)
		{
			PendingQueue.AddFirst((type, message));
		}
	}

	static readonly (NotificationType type, string message)[] TestMessages = new[]
	{
		// Generic
		(NotificationType.Generic, "Un/une objet vous a été give par un admin."),
		// Success
		(NotificationType.Success, "Tu as rendu la clé."),
		(NotificationType.Success, "Vous avez give un objet."),
		// Info
		(NotificationType.Info, "Nouveau Jour"),
		(NotificationType.Info, "Vous avez verrouillé la porte."),
		(NotificationType.Info, "Demande envoyée."),
		(NotificationType.Info, "Cette amende est déjà payée."),
		// Warning
		(NotificationType.Warning, "Veuillez patientez pendant le traitement."),
		(NotificationType.Warning, "Pas de carte bancaire"),
		(NotificationType.Warning, "Quelqu'un utilise déjà cet ATM, veuillez patienter, merci."),
		(NotificationType.Warning, "Le propriétaire s'est déconnecté, accès perdu."),
		// Error
		(NotificationType.Error, "Vous n'avez pas accès a cette commande !"),
		(NotificationType.Error, "Vous n'avez pas assez d'argent."),
		(NotificationType.Error, "Cette porte ne peut pas être achetée."),
		(NotificationType.Error, "Accès refusé !"),
		// Stack test — same message repeated
		(NotificationType.Error, "Vous n'avez pas assez d'argent."),
		(NotificationType.Error, "Vous n'avez pas assez d'argent."),
	};

	[ConCmd( "notif_test" )]
	public static async void TestNotifications()
	{
		foreach (var (type, message) in TestMessages)
		{
			Notify(type, message);
			await GameTask.DelaySeconds( 0.4f );
		}
	}

	/// <summary>
	/// Simule le cas "joueur appuie plusieurs fois E sur une poubelle occupee".
	/// Envoie la meme notif 4 fois avec 0.8s d'ecart : chaque occurrence doit
	/// re-pop visuellement (animation :intro + son ui_press), avec le compteur
	/// qui passe de 1 a x4.
	/// </summary>
	[ConCmd( "notif_test_repeat" )]
	public static async void TestNotifRepeat()
	{
		for ( int i = 0; i < 4; i++ )
		{
			Notify( NotificationType.Error, "Cette poubelle est déjà utilisée par quelqu'un." );
			await GameTask.DelaySeconds( 0.8f );
		}
	}
}
