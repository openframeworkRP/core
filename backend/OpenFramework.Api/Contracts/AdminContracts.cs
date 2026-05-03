namespace OpenFramework.Api.Contracts;

// ── Modération joueurs ────────────────────────────────────────────────────────

public class BanPlayerRequest
{
    public string UserSteamId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class UnbanPlayerRequest
{
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class WhitelistPlayerRequest
{
    public string UserSteamId { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class WarnPlayerRequest
{
    public string UserSteamId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

// ── Personnages ───────────────────────────────────────────────────────────────

public class AdminPatchCharacterRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}

// ── Inventaire ────────────────────────────────────────────────────────────────

public class GiveItemRequest
{
    public string CharacterId { get; set; } = "";
    public string ItemGameId { get; set; } = "";
    public float Mass { get; set; }
    public int Count { get; set; } = 1;
    public Dictionary<string, string>? Metadata { get; set; }
    public int Line { get; set; }
    public int Collum { get; set; }
}

public class PatchItemRequest
{
    public string? ItemGameId { get; set; }
    public float? Mass { get; set; }
    public int? Count { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public int? Line { get; set; }
    public int? Collum { get; set; }
}

// ── MDT ───────────────────────────────────────────────────────────────────────

public class NewCriminalRecordRequest
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string FromWhoMandatedId { get; set; } = "";
}

public class AddFineRequest
{
    public string Id { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime DueAt { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    public string IssuedByCharacterId { get; set; } = "";
}

public class CharacterFinesResult
{
    public string CharacterId { get; set; } = "";
    public string FirstName   { get; set; } = "";
    public string LastName    { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public List<OpenFramework.Api.Models.Mdt.FineRecord> Fines { get; set; } = new();
}
