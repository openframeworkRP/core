namespace OpenFramework.Api.DToS;

public class AddUserBanDto
{
    public string UserSteamId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class RemoveUserBanDto
{
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class AddUserInWhitelistDto
{
    public string UserSteamId { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class AddWarningDto
{
    public string UserSteamId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string AdminSteamId { get; set; } = "";
}

public class AdminGiveItemDto
{
    public string CharacterId { get; set; } = "";
    public string ItemGameId { get; set; } = "";
    public float Mass { get; set; }
    public int Count { get; set; } = 1;
    public Dictionary<string, string>? Metadata { get; set; }
    public int Line { get; set; }
    public int Collum { get; set; }
}

public class AdminModifyItemDto
{
    public string? ItemGameId { get; set; }
    public float? Mass { get; set; }
    public int? Count { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
    public int? Line { get; set; }
    public int? Collum { get; set; }
}

public class AdminUpdateCharacterDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
