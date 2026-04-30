using OpenFramework.Api.Models;

namespace OpenFramework.Api.DToS;

/// <summary>
/// Bloc complet d'apparence d'un personnage. C'est l'unique forme acceptee par
/// l'endpoint PUT /Character/{id}/appearance et l'unique forme retournee dans
/// les GETs (via CharacterResponseDto qui l'inclut).
///
/// Ecriture totale, pas de patch. Le createur, le coiffeur, le futur chir
/// esthetique et tout futur ecriveur doivent envoyer le bloc entier. Cote API
/// on remplace tout d'un coup, pas de logique de "champ null = pas changer" :
/// ca evite les desynchronisations partielles qui ont fait perdre des semaines
/// (couleur OK / morphs perdus / vetements moitie sauves).
///
/// Le format des morphs et vetements suit strictement ce que le jeu envoie :
/// - MorphsJson : dict JSON dont les CLES sont les noms reels du modele citizen
///   (camelCase, ex "browDown_L"). Pas de mapping cote API. Voir MorphCatalog
///   cote core pour la liste autorisee.
/// - ClothingJson : liste JSON des ResourcePath (.clothing) equipes.
/// </summary>
public class CharacterAppearanceDto
{
    public Gender Gender { get; set; }
    public ColorBody ColorBody { get; set; }
    public string MorphsJson { get; set; } = "{}";
    public string ClothingJson { get; set; } = "[]";
    public string HairStyle { get; set; } = "";
    public string BeardStyle { get; set; } = "";
    public string HairColor { get; set; } = "#3a2a1c";
    public string BeardColor { get; set; } = "#3a2a1c";
}
