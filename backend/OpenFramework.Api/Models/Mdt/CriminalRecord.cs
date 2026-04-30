namespace OpenFramework.Api.Models.Mdt;

public class CriminalRecord
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string FromWhoMandatedId { get; set; } // La personne qui donne la peine
    public string ToWhoCharacterId { get; set; }
}