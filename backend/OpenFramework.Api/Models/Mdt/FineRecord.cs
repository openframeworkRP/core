namespace OpenFramework.Api.Models.Mdt;

public class FineRecord
{
    public string Id { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public DateTime DueAt { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = "";
    public bool Paid { get; set; }
    public DateTime? PaidAt { get; set; }
    public string IssuedByCharacterId { get; set; } = "";
}
