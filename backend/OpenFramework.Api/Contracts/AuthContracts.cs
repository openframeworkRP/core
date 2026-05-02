namespace OpenFramework.Api.Contracts;

public class PlayerAuthRequest
{
    public string Id { get; set; } = "";
    public string Token { get; set; } = "";
}

public record ServerAuthRequest(string ServerSecret);

public class TokenValidationResult
{
    public long SteamId { get; set; }
    public string Status { get; set; } = "";
}
