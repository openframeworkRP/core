using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenFramework.Database.DTO;

public class UserDTO : ITableDTO
{
	[TableProperty( true )] public Guid Id { get; set; }

	[TableProperty( true, true )] public ulong SteamId { get; set; }

	[TableProperty] public string Username { get; set; } = string.Empty;

	// Utilise UTC pour toutes les dates
	[TableProperty] public DateTime LastActive { get; set; }
	[TableProperty] public DateTime FirstJoined { get; set; }
	[TableProperty] public DateTime LastLogin { get; set; }

	[TableProperty] public long Bank { get; set; }
	[TableProperty] public long Money { get; set; }

	[TableProperty] public bool IsOnline { get; set; }
	[TableProperty] public bool IsBanned { get; set; }
	[TableProperty] public string BanReason { get; set; } = string.Empty;

	// ⚠️ Garde ça serveur-only côté code (ne le sync pas)
	[TableProperty] public string IPAddress { get; set; } = string.Empty;

	// Si ton moteur DB gère TimeSpan: ok. Sinon -> stocke en long (secondes totales).
	[TableProperty] public TimeSpan PlayTime { get; set; }

	[TableProperty] public List<string> Warnings { get; set; } = new();

	// JobId: préfère un identifiant stable (ex: "police") plutôt qu’un DisplayName
	[TableProperty] public string JobId { get; set; } = string.Empty;
	[TableProperty] public string JobGrade { get; set; } = string.Empty;

	[TableProperty] public DateTime LastXPChange { get; set; }
	[TableProperty] public int SessionXP { get; set; }
	[TableProperty] public Dictionary<TimeSpan, string> BankTransferHistory { get; set; }

	[TableProperty] public Guid HudSettingsId { get; set; }
	[JsonIgnore, ForeignKey(nameof( HudSettingsId ))] public HudSettingsDTO HudSettings { get; set; }
}
