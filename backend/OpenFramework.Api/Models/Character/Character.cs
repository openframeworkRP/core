namespace OpenFramework.Api.Models;

/// <summary>
/// Personnage RP du joueur. L'apparence visuelle complete est portee par cette
/// entite (couleur de peau, morphs, vetements, cheveux/barbe) — il n'y a plus
/// de table separee. Les morphs et vetements sont persistes en JSON pour
/// pouvoir evoluer sans migration : le format est dicte par le jeu (cf
/// MorphCatalog cote core).
///
/// L'index "head" (variante de tete) n'est PAS persiste : il derive de la
/// couleur de peau (Dark=0, Light=1) selon la convention partagee avec le
/// createur de personnage. Si on voulait decoupler un jour, ajouter le champ
/// ici et router l'UI dessus.
/// </summary>
public class Character
{
    public string Id { get; set; }
    public string OwnerId { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country CountryWhereFrom { get; set; }
    public Gender Gender { get; set; }
    public ColorBody ColorBody { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public bool IsSelected { get; set; }
    public string ActualJobIdent { get; set; }

    // Apparence — bloc complet, ecrit/lu en un seul endpoint PUT /appearance.
    public string HairStyle { get; set; } = "";
    public string BeardStyle { get; set; } = "";
    public string HairColor { get; set; } = "#3a2a1c";
    public string BeardColor { get; set; } = "#3a2a1c";

    /// <summary>
    /// Dictionnaire JSON des morphs faciaux : clefs = noms reels du modele
    /// citizen (camelCase, ex "browDown_L"), valeurs = float [0..1]. Pas de
    /// mapping cote API : le jeu serialise/deserialise tel quel via MorphCatalog.
    /// "{}" = pas de morphs custom.
    /// </summary>
    public string MorphsJson { get; set; } = "{}";

    /// <summary>
    /// Liste JSON des ResourcePath des vetements equipes. "[]" = nu.
    /// Persiste tout l'equipement visuel. Le coiffeur, le createur et l'inventaire
    /// poussent tous ce champ via PUT /appearance — un seul flux, plus de RPC
    /// broadcast d'apparence.
    /// </summary>
    public string ClothingJson { get; set; } = "[]";
}

public enum Gender
{
    Male,
    Female
}

public enum ColorBody
{
    Dark,
    Light
}

public enum Country
{
    France,
    Germany,
}
