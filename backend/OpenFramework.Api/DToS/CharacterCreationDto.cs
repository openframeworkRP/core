using OpenFramework.Api.Models;

namespace OpenFramework.Api.DToS;

/// <summary>
/// Payload de creation initiale d'un personnage. Concatene l'identite (etat civil)
/// et l'apparence complete : la creation est atomique, on n'a pas de fenetre
/// "perso cree mais pas encore configure" en base. Apres creation, toute
/// modification d'apparence passe par PUT /Character/{id}/appearance avec
/// le meme bloc CharacterAppearanceDto.
/// </summary>
public class CharacterCreationDto
{
    // Identite RP — definitive a la creation.
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country CountryWhereFrom { get; set; }
    public float Height { get; set; } = 1.75f;
    public float Weight { get; set; } = 70f;
    public string ActualJob { get; set; } = "";

    // Apparence initiale — bloc complet.
    public CharacterAppearanceDto Appearance { get; set; } = new();
}
