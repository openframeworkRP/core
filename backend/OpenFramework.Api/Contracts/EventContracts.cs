namespace OpenFramework.Api.Contracts;

public class PlayerJoinRequest
{
    public string SteamId { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class PlayerLeaveRequest
{
    public string SteamId { get; set; } = "";
    public Guid? SessionId { get; set; }
}

public class ChatMessageRequest
{
    public string SteamId { get; set; } = "";
    public string? AuthorName { get; set; }
    public string? Channel { get; set; }
    public string Message { get; set; } = "";
    public bool IsCommand { get; set; }
}

public class AdminAuditRequest
{
    public string AdminSteamId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? TargetSteamId { get; set; }
    public string? Reason { get; set; }
    public string? PayloadJson { get; set; }
    public string? Source { get; set; }
}

public class InventoryEventRequest
{
    public string ActorSteamId { get; set; } = "";
    public string? CharacterId { get; set; }
    public string Action { get; set; } = "";
    public string? ItemGameId { get; set; }
    public int Count { get; set; }
    public string? SourceType { get; set; }
    public string? SourceId { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? MetadataJson { get; set; }
}

public class InventoryEventBulkRequest
{
    public List<InventoryEventRequest> Logs { get; set; } = new();
}
