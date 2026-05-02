using OpenFramework.Api.Models;

namespace OpenFramework.Api.Contracts;

// ── Création ──────────────────────────────────────────────────────────────────

public class CreateCharacterRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country Origin { get; set; }
    public float Height { get; set; } = 1.75f;
    public float Weight { get; set; } = 70f;
    public string Occupation { get; set; } = "";
    public AppearanceBody Appearance { get; set; } = new();
}

// ── Apparence ─────────────────────────────────────────────────────────────────

public class AppearanceBody
{
    public Gender Gender { get; set; }
    public ColorBody SkinTone { get; set; }
    public string Morphs { get; set; } = "{}";
    public string Clothing { get; set; } = "[]";
    public string HairStyle { get; set; } = "";
    public string BeardStyle { get; set; } = "";
    public string HairColor { get; set; } = "#3a2a1c";
    public string BeardColor { get; set; } = "#3a2a1c";
}

// ── Réponse ───────────────────────────────────────────────────────────────────

public class PlayerCharacterView
{
    public string Id { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public DateTime DateOfBirth { get; set; }
    public Country Origin { get; set; }
    public float Height { get; set; }
    public float Weight { get; set; }
    public string Occupation { get; set; } = "";
    public bool IsActive { get; set; }
    public AppearanceBody Appearance { get; set; } = new();

    public static PlayerCharacterView From(Character c) => new()
    {
        Id = c.Id,
        OwnerId = c.OwnerId,
        FirstName = c.FirstName,
        LastName = c.LastName,
        Age = c.Age,
        DateOfBirth = c.DateOfBirth,
        Origin = c.CountryWhereFrom,
        Height = c.Height,
        Weight = c.Weight,
        Occupation = c.ActualJobIdent,
        IsActive = c.IsSelected,
        Appearance = new AppearanceBody
        {
            Gender = c.Gender,
            SkinTone = c.ColorBody,
            Morphs = c.MorphsJson ?? "{}",
            Clothing = c.ClothingJson ?? "[]",
            HairStyle = c.HairStyle ?? "",
            BeardStyle = c.BeardStyle ?? "",
            HairColor = string.IsNullOrWhiteSpace(c.HairColor) ? "#3a2a1c" : c.HairColor,
            BeardColor = string.IsNullOrWhiteSpace(c.BeardColor) ? "#3a2a1c" : c.BeardColor,
        }
    };
}

// ── Position ──────────────────────────────────────────────────────────────────

public class PositionUpdateRequest
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}
