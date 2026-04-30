using OpenFramework.Api.Models;

namespace OpenFramework.Api.DToS;

/// <summary>
/// Forme de retour pour les endpoints GET /Character/* consommes par le jeu.
/// Identite + Appearance complet en un seul objet : le jeu hydrate Client.*
/// directement depuis cette structure, plus de fetch supplementaire.
/// </summary>
public class CharacterResponseDto
{
    public string Id { get; set; }
    public string OwnerId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country CountryWhereFrom { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public string ActualJobIdent { get; set; }
    public bool IsSelected { get; set; }

    public CharacterAppearanceDto Appearance { get; set; } = new();

    public static CharacterResponseDto From(Character c)
    {
        return new CharacterResponseDto
        {
            Id = c.Id,
            OwnerId = c.OwnerId,
            FirstName = c.FirstName,
            LastName = c.LastName,
            Age = c.Age,
            DateOfBirth = c.DateOfBirth,
            CountryWhereFrom = c.CountryWhereFrom,
            Height = c.Height,
            Weight = c.Weight,
            ActualJobIdent = c.ActualJobIdent,
            IsSelected = c.IsSelected,
            Appearance = new CharacterAppearanceDto
            {
                Gender = c.Gender,
                ColorBody = c.ColorBody,
                MorphsJson = c.MorphsJson ?? "{}",
                ClothingJson = c.ClothingJson ?? "[]",
                HairStyle = c.HairStyle ?? "",
                BeardStyle = c.BeardStyle ?? "",
                HairColor = string.IsNullOrWhiteSpace(c.HairColor) ? "#3a2a1c" : c.HairColor,
                BeardColor = string.IsNullOrWhiteSpace(c.BeardColor) ? "#3a2a1c" : c.BeardColor,
            },
        };
    }
}
