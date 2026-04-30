using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using OpenFramework.Api.DToS;
using OpenFramework.Api.DTOs;

namespace OpenFramework.Api.Services;

public class CharacterService
{
    private readonly OpenFrameworkDbContext _context;
    private readonly BankService _bankService;

    public CharacterService(OpenFrameworkDbContext context, BankService bankService)
    {
        _context = context;
        _bankService = bankService;
    }

    /// <summary>
    /// Cree un personnage avec son identite ET son apparence initiale en une
    /// transaction. Apres ca, l'apparence se modifie via SetAppearanceAsync :
    /// on n'a jamais d'etat "perso cree mais pas configure".
    /// </summary>
    public async Task<Character> CreateCharacterAsync(string ownerId, CharacterCreationDto dto)
    {
        var characterId = Guid.NewGuid().ToString();

        var appearance = dto.Appearance ?? new CharacterAppearanceDto();
        var character = new Character
        {
            Id = characterId,
            OwnerId = ownerId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Age = dto.Age,
            DateOfBirth = dto.DateOfBirth,
            CountryWhereFrom = dto.CountryWhereFrom,
            Height = dto.Height,
            Weight = dto.Weight,
            ActualJobIdent = dto.ActualJob ?? "",
            // Apparence atomique
            Gender = appearance.Gender,
            ColorBody = appearance.ColorBody,
            MorphsJson = string.IsNullOrEmpty(appearance.MorphsJson) ? "{}" : appearance.MorphsJson,
            ClothingJson = string.IsNullOrEmpty(appearance.ClothingJson) ? "[]" : appearance.ClothingJson,
            HairStyle = appearance.HairStyle ?? "",
            BeardStyle = appearance.BeardStyle ?? "",
            HairColor = string.IsNullOrWhiteSpace(appearance.HairColor) ? "#3a2a1c" : appearance.HairColor,
            BeardColor = string.IsNullOrWhiteSpace(appearance.BeardColor) ? "#3a2a1c" : appearance.BeardColor,
        };

        var position = new CharacterPosition
        {
            Id = Guid.NewGuid().ToString(),
            CharacterId = characterId,
            X = 0, Y = 0, Z = 0
        };

        var inventory = new Inventory
        {
            Id = Guid.NewGuid().ToString(),
            OwnerId = characterId
        };

        _context.Characters.Add(character);
        _context.CharacterPositions.Add(position);
        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync();

        var account = await _bankService.CreateAccountAsync(character.Id, new CreateAccountDto(
            "Compte principale de M." + character.LastName,
            AccountType.Personal));

        var initialAmountCents = BankService.ToCents(500);
        account.BalanceCents += initialAmountCents;

        var firstTransaction = new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            AmountCents = initialAmountCents,
            FromAccount = null,
            ToAccount = account,
            CreatedAt = DateTime.UtcNow,
            Comment = "Mamie t'aime " + character.FirstName,
            Type = TransactionType.AdminCreation,
        };

        _context.Transactions.Add(firstTransaction);
        await _context.SaveChangesAsync();

        return character;
    }

    /// <summary>
    /// REMPLACE COMPLETEMENT le bloc d'apparence. Pas de patch partiel : un seul
    /// ecriveur (cote jeu : RequestSetAppearance), un seul format. Si tu as besoin
    /// de ne changer qu'un champ (le coiffeur change la coupe), tu fais un GET
    /// puis un PUT avec le bloc complet modifie. C'est ce qui evite les morphs
    /// perdus / la couleur reset / le clothing dupli qu'on avait avant.
    /// </summary>
    public async Task<bool> SetAppearanceAsync(string ownerId, string id, CharacterAppearanceDto dto)
    {
        var existing = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId);

        if (existing == null) return false;
        if (dto == null) return false;

        existing.Gender = dto.Gender;
        existing.ColorBody = dto.ColorBody;
        existing.MorphsJson = string.IsNullOrEmpty(dto.MorphsJson) ? "{}" : dto.MorphsJson;
        existing.ClothingJson = string.IsNullOrEmpty(dto.ClothingJson) ? "[]" : dto.ClothingJson;
        existing.HairStyle = dto.HairStyle ?? "";
        existing.BeardStyle = dto.BeardStyle ?? "";
        existing.HairColor = string.IsNullOrWhiteSpace(dto.HairColor) ? "#3a2a1c" : dto.HairColor;
        existing.BeardColor = string.IsNullOrWhiteSpace(dto.BeardColor) ? "#3a2a1c" : dto.BeardColor;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Character?> GetByIdAsync(string id)
        => await _context.Characters.FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<Character>> GetAllByOwnerAsync(string ownerId)
        => await _context.Characters.Where(c => c.OwnerId == ownerId).ToListAsync();

    public async Task<CharacterResponseDto?> GetByIdAsResponseAsync(string id)
    {
        var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == id);
        if (character == null) return null;
        return CharacterResponseDto.From(character);
    }

    public async Task<List<CharacterResponseDto>> GetAllByOwnerAsResponseAsync(string ownerId)
    {
        var characters = await _context.Characters
            .Where(c => c.OwnerId == ownerId)
            .ToListAsync();
        return characters.Select(CharacterResponseDto.From).ToList();
    }

    public async Task<CharacterResponseDto?> GetSelectedCharacterAsResponseAsync(string steamId)
    {
        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.OwnerId == steamId && c.IsSelected);
        if (character == null) return null;
        return CharacterResponseDto.From(character);
    }

    public async Task<bool> DeleteCharacterAsync(string characterId, string ownerId)
    {
        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerId == ownerId);

        if (character == null) return false;

        _context.Characters.Remove(character);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Suppression admin (sans contrôle de propriétaire) — pour les noms RP
    /// trolls / inappropriés signalés depuis le panel web.
    /// </summary>
    public async Task<bool> AdminDeleteCharacterAsync(string characterId)
    {
        var character = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId);

        if (character == null) return false;

        _context.Characters.Remove(character);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Mise à jour admin partielle (FirstName / LastName) sans contrôle de
    /// propriétaire. Seuls les champs non-null du DTO sont écrits.
    /// </summary>
    public async Task<Character?> AdminUpdateCharacterAsync(string characterId, string? firstName, string? lastName)
    {
        var existing = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId);
        if (existing == null) return null;

        if (!string.IsNullOrWhiteSpace(firstName)) existing.FirstName = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName))  existing.LastName  = lastName.Trim();

        await _context.SaveChangesAsync();
        return existing;
    }

    public async Task<List<Character>> GetCharactersByOwnerAsync(string ownerId)
    {
        return await _context.Characters
            .Where(c => c.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task<CharacterPosition?> GetPositionAsync(string characterId)
    {
        return await _context.CharacterPositions
            .FirstOrDefaultAsync(p => p.CharacterId == characterId);
    }

    public async Task<bool> UpdatePositionAsync(string characterId, int x, int y, int z)
    {
        var pos = await _context.CharacterPositions.FirstOrDefaultAsync(p => p.CharacterId == characterId);
        if (pos == null) return false;

        pos.X = x; pos.Y = y; pos.Z = z;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateCharacterAsync(string ownerId, string id, Character updatedChar)
    {
        var existing = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId);

        if (existing == null) return false;

        existing.FirstName = updatedChar.FirstName;
        existing.LastName = updatedChar.LastName;
        existing.Age = updatedChar.Age;
        existing.Height = updatedChar.Height;
        existing.Weight = updatedChar.Weight;
        existing.ActualJobIdent = updatedChar.ActualJobIdent;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Character>> GetByNameAsync(string name)
    {
        return await _context.Characters
            .Where(c => c.FirstName.Contains(name) || c.LastName.Contains(name))
            .ToListAsync();
    }

    public async Task<Character?> GetSelectedCharacterAsync(string steamId)
    {
        return await _context.Characters
            .FirstOrDefaultAsync(c => c.OwnerId == steamId && c.IsSelected);
    }

    /// <summary>
    /// Sélectionne le personnage {characterId} pour ce steamId.
    /// Désélectionne automatiquement l'éventuel personnage actif précédent.
    /// </summary>
    public async Task<bool> SelectCharacterAsync(string steamId, string characterId)
    {
        var target = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerId == steamId);

        if (target == null) return false;

        var current = await _context.Characters
            .FirstOrDefaultAsync(c => c.OwnerId == steamId && c.IsSelected);

        if (current != null && current.Id != characterId)
            current.IsSelected = false;

        target.IsSelected = true;

        await _context.SaveChangesAsync();
        return true;
    }
}
