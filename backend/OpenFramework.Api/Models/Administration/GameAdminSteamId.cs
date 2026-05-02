using System.ComponentModel.DataAnnotations;

namespace OpenFramework.Api.Models.Administration;

public class GameAdminSteamId
{
    [Key]
    public string SteamId { get; set; } = "";
}
