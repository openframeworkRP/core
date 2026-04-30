namespace OpenFramework.Utility;
public static class TimeUtils
{
	public enum Style
	{
		Long,    // "2 minutes 5 secondes"
		Court,   // "2m05s"
		Horloge  // "02:05" ou "01:02:05" si >= 1h
	}

	/// <summary>
	/// Convertit un délai (secondes) en texte lisible FR.
	/// -1, NaN, +∞ => "indéfiniment"
	/// </summary>
	public static string DelayToReadable( float seconds, Style format = Style.Long )
	{
		if ( float.IsNaN( seconds ) || float.IsPositiveInfinity( seconds ) || seconds < 0f )
			return "indéfiniment";

		// Arrondi vers le haut (ex: 59.3 -> 60) pour rester intuitif.
		int total = (int)MathF.Ceiling( seconds );

		int days = total / 86400; total %= 86400;
		int hours = total / 3600; total %= 3600;
		int mins = total / 60;
		int secs = total % 60;

		switch ( format )
		{
			case Style.Horloge:
				if ( days > 0 ) return $"{days:00}:{hours:00}:{mins:00}:{secs:00}";
				if ( hours > 0 ) return $"{hours:00}:{mins:00}:{secs:00}";
				return $"{mins:00}:{secs:00}";

			case Style.Court:
				{
					// Exemple: "1j 02h", "2h05m", "3m07s", "45s"
					if ( days > 0 ) return hours > 0 ? $"{days}j {hours:00}h" : $"{days}j";
					if ( hours > 0 ) return $"{hours}h{mins:00}m";
					if ( mins > 0 ) return $"{mins}m{secs:00}s";
					return $"{secs}s";
				}

			default: // Style.Long
				{
					// Exemple: "1 jour 2 heures", "2 heures 5 minutes", "3 minutes 7 secondes", "45 secondes"
					var parts = new List<string>( 3 );
					if ( days > 0 ) parts.Add( PluralFr( days, "jour", "jours" ) );
					if ( hours > 0 ) parts.Add( PluralFr( hours, "heure", "heures" ) );
					if ( mins > 0 ) parts.Add( PluralFr( mins, "minute", "minutes" ) );
					if ( parts.Count == 0 ) // moins d’une minute -> secondes quand même
						return PluralFr( secs, "seconde", "secondes" );
					if ( secs > 0 ) parts.Add( PluralFr( secs, "seconde", "secondes" ) );
					return string.Join( " ", parts );
				}
		}
	}

	private static string PluralFr( int n, string singulier, string pluriel )
	{
		// En français, 0 prend le pluriel
		return n == 1 ? $"1 {singulier}" : $"{n} {pluriel}";
	}
}
