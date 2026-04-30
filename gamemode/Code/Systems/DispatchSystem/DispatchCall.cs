namespace OpenFramework.Systems.Dispatch;

/// <summary>
/// Représente un appel de dispatch en cours ou archivé.
/// Synced depuis le host vers tous les clients concernés.
/// </summary>
public sealed class DispatchCall
{
	/// <summary>ID unique de l'appel.</summary>
	public int Id { get; set; }

	/// <summary>Type d'urgence.</summary>
	public DispatchType Type { get; set; }

	/// <summary>Nom affiché de l'appelant (ou "Anonyme").</summary>
	public string CallerName { get; set; }

	/// <summary>Position de l'incident.</summary>
	public Vector3 Position { get; set; }

	/// <summary>Description courte de la situation.</summary>
	public string Description { get; set; }

	/// <summary>Timestamp de création (Time.Now).</summary>
	public float CreatedAt { get; set; }

	/// <summary>Statut courant de l'appel.</summary>
	public DispatchStatus Status { get; set; } = DispatchStatus.Pending;

	/// <summary>Nom du répondant qui a accepté (null si personne).</summary>
	public string AcceptedBy { get; set; }

	/// <summary>Cible (Police / EMS / Both).</summary>
	public DispatchTarget Target => Type.GetTarget();

	/// <summary>Temps écoulé depuis la création en secondes.</summary>
	public float ElapsedSeconds => Time.Now - CreatedAt;

	public string ElapsedLabel
	{
		get
		{
			int secs = (int)ElapsedSeconds;
			if ( secs < 60 ) return $"{secs}s";
			return $"{secs / 60}m{secs % 60:00}s";
		}
	}
}

public enum DispatchStatus
{
	Pending,   // En attente de réponse
	Accepted,  // Quelqu'un a accepté
	Closed,    // Clôturé
}
