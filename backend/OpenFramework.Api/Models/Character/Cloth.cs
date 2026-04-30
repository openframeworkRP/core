namespace OpenFramework.Api.Models;

public class Cloth
{
    public string Id { get; set; }
    public string OwnerId { get; set; }
    public string IdentCloth { get; set; }
    public ClothType Type { get; set; }
}

public enum ClothType
{
    Haut,
    CouvreChef,
    Bas,
    Chaussure
}