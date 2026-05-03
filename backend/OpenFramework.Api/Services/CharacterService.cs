using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class CharacterService
{
    private readonly OpenFrameworkDbContext _context;
    private readonly BankService _bankService;
    private readonly CacheService _cache;

    private static readonly TimeSpan CharTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(2);

    public CharacterService(OpenFrameworkDbContext context, BankService bankService, CacheService cache)
    {
        _context = context;
        _bankService = bankService;
        _cache = cache;
    }

    public async Task<Character> CreateCharacterAsync(string ownerId, CreateCharacterRequest request)
    {
        var characterId = Guid.NewGuid().ToString();
        var appearance = request.Appearance ?? new AppearanceBody();

        var character = new Character
        {
            Id = characterId,
            OwnerId = ownerId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Age = request.Age,
            DateOfBirth = request.DateOfBirth,
            CountryWhereFrom = request.Origin,
            Height = request.Height,
            Weight = request.Weight,
            ActualJobIdent = request.Occupation,
            Gender = appearance.Gender,
            ColorBody = appearance.SkinTone,
            MorphsJson = string.IsNullOrEmpty(appearance.Morphs) ? "{}" : appearance.Morphs,
            ClothingJson = string.IsNullOrEmpty(appearance.Clothing) ? "[]" : appearance.Clothing,
            HairStyle = appearance.HairStyle ?? "",
            BeardStyle = appearance.BeardStyle ?? "",
            HairColor = string.IsNullOrWhiteSpace(appearance.HairColor) ? "#3a2a1c" : appearance.HairColor,
            BeardColor = string.IsNullOrWhiteSpace(appearance.BeardColor) ? "#3a2a1c" : appearance.BeardColor,
        };

        var position = new CharacterPosition { Id = Guid.NewGuid().ToString(), CharacterId = characterId, X = 0, Y = 0, Z = 0 };
        var inventory = new Inventory { Id = Guid.NewGuid().ToString(), OwnerId = characterId };

        _context.Characters.Add(character);
        _context.CharacterPositions.Add(position);
        _context.Inventories.Add(inventory);
        await _context.SaveChangesAsync();

        var account = await _bankService.CreateAccountAsync(character.Id, new OpenAccountRequest(
            "Compte principal de M." + character.LastName, AccountType.Personal));

        var initialAmountCents = BankService.ToCents(500);
        account.BalanceCents += initialAmountCents;

        _context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid().ToString(),
            AmountCents = initialAmountCents,
            ToAccount = account,
            CreatedAt = DateTime.UtcNow,
            Comment = "Allocation d'installation",
            Type = TransactionType.AdminCreation,
        });
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(CacheService.CharsOwnerKey(ownerId));
        return character;
    }

    public async Task<bool> SetAppearanceAsync(string ownerId, string id, AppearanceBody body)
    {
        var existing = await _context.Characters
            .FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId);
        if (existing == null || body == null) return false;

        existing.Gender = body.Gender;
        existing.ColorBody = body.SkinTone;
        if (!string.IsNullOrEmpty(body.Morphs) && body.Morphs != "{}")
            existing.MorphsJson = body.Morphs;
        if (!string.IsNullOrEmpty(body.Clothing) && body.Clothing != "[]")
            existing.ClothingJson = body.Clothing;
        existing.HairStyle = body.HairStyle ?? "";
        existing.BeardStyle = body.BeardStyle ?? "";
        existing.HairColor = string.IsNullOrWhiteSpace(body.HairColor) ? "#3a2a1c" : body.HairColor;
        existing.BeardColor = string.IsNullOrWhiteSpace(body.BeardColor) ? "#3a2a1c" : body.BeardColor;

        await _context.SaveChangesAsync();
        await _cache.RemoveManyAsync(CacheService.CharKey(id), CacheService.CharsOwnerKey(ownerId), CacheService.SelectedKey(ownerId));
        return true;
    }

    public async Task<Character?> GetByIdAsync(string id)
        => await _context.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public async Task<List<Character>> GetAllByOwnerAsync(string ownerId)
        => await _context.Characters.AsNoTracking().Where(c => c.OwnerId == ownerId).ToListAsync();

    public async Task<PlayerCharacterView?> GetByIdAsResponseAsync(string id)
    {
        var cached = await _cache.GetAsync<PlayerCharacterView>(CacheService.CharKey(id));
        if (cached != null) return cached;

        var c = await _context.Characters.AsNoTracking().FirstOrDefaultAsync(ch => ch.Id == id);
        if (c == null) return null;

        var view = PlayerCharacterView.From(c);
        await _cache.SetAsync(CacheService.CharKey(id), view, CharTtl);
        return view;
    }

    public async Task<List<PlayerCharacterView>> GetAllByOwnerAsResponseAsync(string ownerId)
    {
        var cached = await _cache.GetAsync<List<PlayerCharacterView>>(CacheService.CharsOwnerKey(ownerId));
        if (cached != null) return cached;

        var characters = await _context.Characters.AsNoTracking().Where(c => c.OwnerId == ownerId).ToListAsync();
        var views = characters.Select(PlayerCharacterView.From).ToList();
        await _cache.SetAsync(CacheService.CharsOwnerKey(ownerId), views, ListTtl);
        return views;
    }

    public async Task<PlayerCharacterView?> GetSelectedCharacterAsResponseAsync(string steamId)
    {
        var cached = await _cache.GetAsync<PlayerCharacterView>(CacheService.SelectedKey(steamId));
        if (cached != null) return cached;

        var c = await _context.Characters.AsNoTracking().FirstOrDefaultAsync(ch => ch.OwnerId == steamId && ch.IsSelected);
        if (c == null) return null;

        var view = PlayerCharacterView.From(c);
        await _cache.SetAsync(CacheService.SelectedKey(steamId), view, ListTtl);
        return view;
    }

    public async Task<bool> DeleteCharacterAsync(string characterId, string ownerId)
    {
        var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == characterId && c.OwnerId == ownerId);
        if (character == null) return false;

        _context.Characters.Remove(character);
        await _context.SaveChangesAsync();
        await _cache.RemoveManyAsync(CacheService.CharKey(characterId), CacheService.CharsOwnerKey(ownerId), CacheService.SelectedKey(ownerId));
        return true;
    }

    public async Task<bool> AdminDeleteCharacterAsync(string characterId)
    {
        var character = await _context.Characters.FirstOrDefaultAsync(c => c.Id == characterId);
        if (character == null) return false;

        var ownerId = character.OwnerId;
        _context.Characters.Remove(character);
        await _context.SaveChangesAsync();
        await _cache.RemoveManyAsync(CacheService.CharKey(characterId), CacheService.CharsOwnerKey(ownerId), CacheService.SelectedKey(ownerId));
        return true;
    }

    public async Task<Character?> AdminUpdateCharacterAsync(string characterId, string? firstName, string? lastName)
    {
        var existing = await _context.Characters.FirstOrDefaultAsync(c => c.Id == characterId);
        if (existing == null) return null;

        if (!string.IsNullOrWhiteSpace(firstName)) existing.FirstName = firstName.Trim();
        if (!string.IsNullOrWhiteSpace(lastName))  existing.LastName  = lastName.Trim();

        await _context.SaveChangesAsync();
        await _cache.RemoveManyAsync(CacheService.CharKey(characterId), CacheService.CharsOwnerKey(existing.OwnerId), CacheService.SelectedKey(existing.OwnerId));
        return existing;
    }

    public async Task<List<Character>> GetCharactersByOwnerAsync(string ownerId)
        => await _context.Characters.AsNoTracking().Where(c => c.OwnerId == ownerId).ToListAsync();

    public async Task<CharacterPosition?> GetPositionAsync(string characterId)
        => await _context.CharacterPositions.AsNoTracking().FirstOrDefaultAsync(p => p.CharacterId == characterId);

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
        var existing = await _context.Characters.FirstOrDefaultAsync(c => c.Id == id && c.OwnerId == ownerId);
        if (existing == null) return false;

        existing.FirstName = updatedChar.FirstName;
        existing.LastName = updatedChar.LastName;
        existing.Age = updatedChar.Age;
        existing.Height = updatedChar.Height;
        existing.Weight = updatedChar.Weight;
        existing.ActualJobIdent = updatedChar.ActualJobIdent;

        await _context.SaveChangesAsync();
        await _cache.RemoveManyAsync(CacheService.CharKey(id), CacheService.CharsOwnerKey(ownerId), CacheService.SelectedKey(ownerId));
        return true;
    }

    public async Task<List<Character>> GetByNameAsync(string name)
        => await _context.Characters.AsNoTracking()
            .Where(c => c.FirstName.Contains(name) || c.LastName.Contains(name))
            .ToListAsync();

    public async Task<Character?> GetSelectedCharacterAsync(string steamId)
        => await _context.Characters.AsNoTracking().FirstOrDefaultAsync(c => c.OwnerId == steamId && c.IsSelected);

    public async Task<bool> SelectCharacterAsync(string steamId, string characterId)
    {
        var affected = await _context.Characters
            .Where(c => c.OwnerId == steamId && (c.Id == characterId || c.IsSelected))
            .ToListAsync();

        var target = affected.FirstOrDefault(c => c.Id == characterId);
        if (target == null) return false;

        var current = affected.FirstOrDefault(c => c.IsSelected && c.Id != characterId);
        if (current != null) current.IsSelected = false;
        target.IsSelected = true;

        await _context.SaveChangesAsync();

        var keys = new List<string> { CacheService.CharsOwnerKey(steamId), CacheService.SelectedKey(steamId), CacheService.CharKey(characterId) };
        if (current != null) keys.Add(CacheService.CharKey(current.Id));
        await _cache.RemoveManyAsync([.. keys]);
        return true;
    }
}
