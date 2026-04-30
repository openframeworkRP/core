using System;
using OpenFramework.Api;
using static OpenFramework.IdCardPage;

namespace OpenFramework;

/// <summary>
/// Singleton statique qui centralise toutes les données de création de personnage.
/// Persiste entre les pages du MenuCreatorCharacter et est vidé après validation.
/// </summary>
public static class CharacterCreationState 
{
	// ─── Étape 1 : Identité (IdCardPage) ──────────────────────────────────────

	public static string FirstName { get; set; } = "";
	public static string LastName { get; set; } = "";

	public static int BirthDay { get; set; } = 1;
	public static int BirthMonth { get; set; } = 1;
	public static int BirthYears { get; set; } = 1990;
	public static Color Color { get; set; }

	public static CountryList BirthCountry { get; set; } = CountryList.France;
	public static CityList BirthCity { get; set; } = CityList.Alabama;
	public static ColorBody  ColorBody { get; set; } = ColorBody.Dark;

	// ─── Étape 2 : Corps (CreatorPage) ────────────────────────────────────────

	public static bool IsFemale { get; set; } = false;
	public static int HeadIndex { get; set; } = 0;

	// Morphs faciaux
	public static float BrowDown_L { get; set; } = 0f;
	public static float BrowDown_R { get; set; } = 0f;
	public static float BrowInnerUp { get; set; } = 0f;
	public static float BrowOuterUp_L { get; set; } = 0f;
	public static float BrowOuterUp_R { get; set; } = 0f;
	public static float EyeLookDown_L { get; set; } = 0f;
	public static float EyeLookDown_R { get; set; } = 0f;
	public static float EyeLookIn_L { get; set; } = 0f;
	public static float EyeLookIn_R { get; set; } = 0f;
	public static float EyeLookOut_L { get; set; } = 0f;
	public static float EyeLookOut_R { get; set; } = 0f;
	public static float EyeLookUp_L { get; set; } = 0f;
	public static float EyeLookUp_R { get; set; } = 0f;
	public static float EyeSquint_L { get; set; } = 0f;
	public static float EyeSquint_R { get; set; } = 0f;
	public static float EyeWide_L { get; set; } = 0f;
	public static float EyeWide_R { get; set; } = 0f;
	public static float CheekPuff { get; set; } = 0f;
	public static float CheekSquint_L { get; set; } = 0f;
	public static float CheekSquint_R { get; set; } = 0f;
	public static float NoseSneer_L { get; set; } = 0f;
	public static float NoseSneer_R { get; set; } = 0f;
	public static float JawForward { get; set; } = 0f;
	public static float JawLeft { get; set; } = 0f;
	public static float JawRight { get; set; } = 0f;
	public static float MouthDimple_L { get; set; } = 0f;
	public static float MouthDimple_R { get; set; } = 0f;
	public static float MouthRollUpper { get; set; } = 0f;
	public static float MouthStretch_L { get; set; } = 0f;
	public static float MouthStretch_R { get; set; } = 0f;

	// ─── Étape 3 : Vêtements (AppearancePage) ────────────────────────────────

	/// <summary>
	/// Liste des ResourceName des ItemMetadata (clothing) choisis au créateur.
	/// </summary>
	public static List<string> SelectedClothingItems { get; set; } = new();

	// ─── Helpers ──────────────────────────────────────────────────────────────

	/// <summary>
	/// Calcule l'âge à partir de la date de naissance stockée.
	/// </summary>
	public static int ComputeAge()
	{
		var today = DateTime.Today;
		var age = today.Year - BirthYears;
		if ( today.Month < BirthMonth || (today.Month == BirthMonth && today.Day < BirthDay) )
			age--;
		return age;
	}

	/// <summary>
	/// Convertit le CountryList en Country (enum API).
	/// </summary>
	public static Country ToApiCountry()
	{
		return BirthCountry switch
		{
			CountryList.Allemagne => Country.Germany,
			_ => Country.France
		};
	}

	/// <summary>
	/// Construit le DTO prêt à envoyer à l'API depuis les données stockées.
	/// </summary>
	public static CharacterCreationDto BuildDto()
	{
		return new CharacterCreationDto
		{
			// Identité
			FirstName = FirstName,
			LastName = LastName,
			Age = ComputeAge(),
			DateOfBirth = new DateTime( BirthYears, BirthMonth, BirthDay ),
			Gender = IsFemale ? Gender.Female : Gender.Male,
			CountryWhereFrom = ToApiCountry(),
			Color = ColorBody,

			// Physique (valeurs par défaut — à brancher sur CharacterManager si besoin)
			Height = 1.75f,
			Weight = 70f,

			// Morphs — on fait la moyenne L/R pour les props symétriques de l'API
			BrowDown = (BrowDown_L + BrowDown_R) / 2f,
			BrowInnerUp = BrowInnerUp,
			BrowOuterUp = (BrowOuterUp_L + BrowOuterUp_R) / 2f,
			EyesLookDown = (EyeLookDown_L + EyeLookDown_R) / 2f,
			EyesLookIn = (EyeLookIn_L + EyeLookIn_R) / 2f,
			EyesLookOut = (EyeLookOut_L + EyeLookOut_R) / 2f,
			EyesLookUp = (EyeLookUp_L + EyeLookUp_R) / 2f,
			EyesSquint = (EyeSquint_L + EyeSquint_R) / 2f,
			EyesWide = (EyeWide_L + EyeWide_R) / 2f,
			CheekPuff = CheekPuff,
			CheekSquint = (CheekSquint_L + CheekSquint_R) / 2f,
			NoseSneer = (NoseSneer_L + NoseSneer_R) / 2f,
			JawForward = JawForward,
			JawLeft = JawLeft,
			JawRight = JawRight,
			MouthDimple = (MouthDimple_L + MouthDimple_R) / 2f,
			MouthRollUpper = MouthRollUpper,
			MouthStretch = (MouthStretch_L + MouthStretch_R) / 2f,
		};
	}

	/// <summary>
	/// Remet tout à zéro après validation (évite des données stale si l'UI est rechargée).
	/// </summary>
	public static void Reset()
	{
		FirstName = "";
		LastName = "";
		BirthDay = 1;
		BirthMonth = 1;
		BirthYears = 1990;
		BirthCountry = CountryList.France;
		BirthCity = CityList.Alabama;
		IsFemale = false;
		HeadIndex = 0;
		ColorBody = ColorBody.Dark;
		BrowDown_L = BrowDown_R = BrowInnerUp = BrowOuterUp_L = BrowOuterUp_R = 0f;
		EyeLookDown_L = EyeLookDown_R = EyeLookIn_L = EyeLookIn_R = 0f;
		EyeLookOut_L = EyeLookOut_R = EyeLookUp_L = EyeLookUp_R = 0f;
		EyeSquint_L = EyeSquint_R = EyeWide_L = EyeWide_R = 0f;
		CheekPuff = CheekSquint_L = CheekSquint_R = 0f;
		NoseSneer_L = NoseSneer_R = 0f;
		JawForward = JawLeft = JawRight = 0f;
		MouthDimple_L = MouthDimple_R = MouthRollUpper = MouthStretch_L = MouthStretch_R = 0f;
		SelectedClothingItems.Clear();
	}
}
