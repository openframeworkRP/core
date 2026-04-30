namespace OpenFramework.Systems.Dispatch;

public enum DispatchType
{
	// Urgences médicales
	MedicalEmergency,   // Urgence médicale générale
	Overdose,           // Overdose
	Accident,           // Accident de la route

	// Criminalité
	Fight,              // Bagarre / Rixe
	Shooting,           // Coups de feu
	Robbery,            // Vol / Braquage
	Hostage,            // Prise d'otage
	Burglary,           // Cambriolage

	// Divers
	SuspiciousActivity, // Activité suspecte
	PanicButton,        // Bouton panique
	CustomCall,         // Appel générique
}

public static class DispatchTypeExtensions
{
	public static string ToLabel( this DispatchType type ) => type switch
	{
		DispatchType.MedicalEmergency   => "🚑 Urgence Médicale",
		DispatchType.Overdose           => "💊 Overdose",
		DispatchType.Accident           => "🚗 Accident",
		DispatchType.Fight              => "👊 Bagarre",
		DispatchType.Shooting           => "🔫 Coups de Feu",
		DispatchType.Robbery            => "💰 Vol / Braquage",
		DispatchType.Hostage            => "🎯 Prise d'Otage",
		DispatchType.Burglary           => "🏠 Cambriolage",
		DispatchType.SuspiciousActivity => "👀 Activité Suspecte",
		DispatchType.PanicButton        => "🆘 Bouton Panique",
		DispatchType.CustomCall         => "📞 Appel Général",
		_                               => type.ToString()
	};

	/// <summary>
	/// Emoji court (pour waypoint minimap, HUD GPS, etc.).
	/// </summary>
	public static string ToEmoji( this DispatchType type ) => type switch
	{
		DispatchType.MedicalEmergency   => "🚑",
		DispatchType.Overdose           => "💊",
		DispatchType.Accident           => "🚗",
		DispatchType.Fight              => "👊",
		DispatchType.Shooting           => "🔫",
		DispatchType.Robbery            => "💰",
		DispatchType.Hostage            => "🎯",
		DispatchType.Burglary           => "🏠",
		DispatchType.SuspiciousActivity => "👀",
		DispatchType.PanicButton        => "🆘",
		DispatchType.CustomCall         => "📞",
		_                               => "🚨"
	};

	/// <summary>
	/// Détermine à qui ce type de dispatch est destiné.
	/// </summary>
	public static DispatchTarget GetTarget( this DispatchType type ) => type switch
	{
		DispatchType.MedicalEmergency => DispatchTarget.EMS,
		DispatchType.Overdose         => DispatchTarget.EMS,
		DispatchType.Accident         => DispatchTarget.Both,
		DispatchType.Fight            => DispatchTarget.Police,
		DispatchType.Shooting         => DispatchTarget.Police,
		DispatchType.Robbery          => DispatchTarget.Police,
		DispatchType.Hostage          => DispatchTarget.Police,
		DispatchType.Burglary         => DispatchTarget.Police,
		DispatchType.SuspiciousActivity => DispatchTarget.Police,
		DispatchType.PanicButton      => DispatchTarget.Both,
		DispatchType.CustomCall       => DispatchTarget.Both,
		_                             => DispatchTarget.Both
	};
}

public enum DispatchTarget
{
	Police,
	EMS,
	Both,
}
