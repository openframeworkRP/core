using OpenFramework.Extension;

namespace OpenFramework.World.Devices.Apps.Phone;

/// <summary>
/// Représente un contact dans l'app téléphone.
/// Peut être un contact fixe (service) ou un joueur connecté.
/// </summary>
public class PhoneContact
{
	public string Id { get; set; }
	public string Name { get; set; }
	public string Number { get; set; }
	public string Emoji { get; set; } = "👤";
	public string AvatarColor { get; set; } = "#555";
	public bool IsService { get; set; } = false;

	/// <summary>
	/// Si c'est un contact joueur, référence vers son Client.
	/// Null pour les contacts fixes.
	/// </summary>
	public Client PlayerClient { get; set; }

	// ── Contacts fixes (services d'urgence) ──────
	public static readonly List<PhoneContact> Services = new()
	{
		new() { Id = "police",   Name = "Police", Number = "17", Emoji = "👮", AvatarColor = "#1a3a6b", IsService = true },
		new() { Id = "ems",     Name = "Ems",             Number = "15", Emoji = "🚑", AvatarColor = "#8b0000", IsService = true },
		new() { Id = "pompiers", Name = "Pompiers",         Number = "18", Emoji = "🚒", AvatarColor = "#8b2000", IsService = true },
		new() { Id = "mairie",   Name = "Mairie",           Number = "30", Emoji = "🏛️", AvatarColor = "#2d4a1e", IsService = true },
	};

	/// <summary>
	/// Construit la liste des contacts joueurs depuis les connexions actives.
	/// </summary>
	public static List<PhoneContact> GetPlayerContacts()
	{
		var list = new List<PhoneContact>();
		var self = Client.Local;

		foreach ( var connection in Connection.All )
		{
			var client = connection.GetClient();
			if ( client == null || client == self ) continue;

			var firstName = client.Data?.FirstName ?? "Inconnu";
			var lastName = client.Data?.LastName ?? "";
			var fullName = $"{firstName} {lastName}".Trim();

			list.Add( new PhoneContact
			{
				Id = connection.SteamId.ToString(),
				Name = fullName,
				Number = connection.SteamId.ToString(),
				Emoji = GetInitials( fullName ),
				AvatarColor = GetColorFromName( fullName ),
				IsService = false,
				PlayerClient = client,
			} );
		}

		return list.OrderBy( x => x.Name ).ToList();
	}

	private static string GetInitials( string name )
	{
		var parts = name.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		return parts.Length >= 2
			? $"{parts[0][0]}{parts[1][0]}".ToUpper()
			: name.Length > 0 ? name[0].ToString().ToUpper() : "?";
	}

	private static string GetColorFromName( string name )
	{
		// Couleur déterministe basée sur le nom
		var colors = new[]
		{
			"#c0392b", "#e67e22", "#f39c12", "#27ae60",
			"#16a085", "#2980b9", "#8e44ad", "#2c3e50"
		};
		int hash = name.GetHashCode();
		return colors[Math.Abs( hash ) % colors.Length];
	}
}
