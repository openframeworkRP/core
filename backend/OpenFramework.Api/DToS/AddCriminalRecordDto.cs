namespace OpenFramework.Api.DToS;

public class AddCriminalRecordDto
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string FromWhoMandatedId { get; set; } // La personne qui donne la peine
}