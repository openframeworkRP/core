namespace OpenFramework.Api.Models.Administration;

public class UserWarn
{
    public string Id { get; set; }
    public string SteamId { get; set; }
    public string Reason { get; set; }
    public string FromAdminSteamId { get; set; }
}