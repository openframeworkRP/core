using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models.Mdt;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/mdt")]
[Authorize(Roles = "GameServer")]
public class MdtController : Controller
{
    private readonly OpenFrameworkDbContext _dbContext;

    public MdtController(OpenFrameworkDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("criminalrecord/{characterId}")]
    public IActionResult GetCriminalRecords(string characterId)
    {
        var records = _dbContext.CriminalRecords.Where(t => t.ToWhoCharacterId == characterId).ToList();

        if (!records.Any())
            return Ok(new { success = true, message = "L'individu n'a pas de casier judiciaire." });

        return Ok(new { success = true, records });
    }

    [HttpPost("criminalrecord/{characterId}/addrecord")]
    public async Task<IActionResult> AddRecord(string characterId, [FromBody] NewCriminalRecordRequest request)
    {
        var record = new CriminalRecord
        {
            Id = Guid.NewGuid().ToString(),
            ToWhoCharacterId = characterId,
            FromWhoMandatedId = request.FromWhoMandatedId,
            Description = request.Description,
            Title = request.Title,
        };

        _dbContext.Add(record);
        await _dbContext.SaveChangesAsync();
        return Ok(new { success = true, record });
    }

    [HttpPost("criminalrecord/{characterId}/removerecord")]
    public Task<IActionResult> RemoveRecord(string characterId)
        => throw new NotImplementedException("à faire");

    // ── Recherche personnage ─────────────────────────────────────────────────

    [HttpGet("search")]
    public IActionResult SearchCharacters([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return BadRequest(new { error = "La recherche doit contenir au moins 2 caractères." });

        var lower = query.ToLower();

        var characters = _dbContext.Characters
            .Where(c => c.FirstName.ToLower().Contains(lower) || c.LastName.ToLower().Contains(lower))
            .Take(20)
            .ToList();

        var results = characters.Select(c =>
        {
            var fines = JsonSerializer.Deserialize<List<FineRecord>>(
                c.FinesJson ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            return new CharacterFinesResult
            {
                CharacterId = c.Id,
                FirstName   = c.FirstName,
                LastName    = c.LastName,
                DateOfBirth = c.DateOfBirth.ToString("yyyy-MM-dd"),
                Fines       = fines,
            };
        }).ToList();

        return Ok(new { success = true, results });
    }

    // ── Amendes ──────────────────────────────────────────────────────────────

    [HttpGet("fines/{characterId}")]
    public IActionResult GetFines(string characterId)
    {
        var character = _dbContext.Characters.Find(characterId);
        if (character == null) return NotFound(new { error = "Personnage introuvable." });

        var fines = JsonSerializer.Deserialize<List<FineRecord>>(
            character.FinesJson ?? "[]",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        return Ok(new { success = true, fines });
    }

    [HttpPost("fines/{characterId}/add")]
    public async Task<IActionResult> AddFine(string characterId, [FromBody] AddFineRequest request)
    {
        var character = await _dbContext.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Personnage introuvable." });

        var fines = JsonSerializer.Deserialize<List<FineRecord>>(
            character.FinesJson ?? "[]",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var record = new FineRecord
        {
            Id                   = string.IsNullOrEmpty(request.Id) ? Guid.NewGuid().ToString() : request.Id,
            IssuedAt             = request.IssuedAt,
            DueAt                = request.DueAt,
            Amount               = request.Amount,
            Reason               = request.Reason,
            Paid                 = false,
            IssuedByCharacterId  = request.IssuedByCharacterId,
        };

        fines.Add(record);
        character.FinesJson = JsonSerializer.Serialize(fines);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true, fine = record });
    }

    [HttpPost("fines/{characterId}/{fineId}/pay")]
    public async Task<IActionResult> PayFine(string characterId, string fineId)
    {
        var character = await _dbContext.Characters.FindAsync(characterId);
        if (character == null) return NotFound(new { error = "Personnage introuvable." });

        var fines = JsonSerializer.Deserialize<List<FineRecord>>(
            character.FinesJson ?? "[]",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

        var fine = fines.FirstOrDefault(f => f.Id == fineId);
        if (fine == null) return NotFound(new { error = "Amende introuvable." });

        fine.Paid  = true;
        fine.PaidAt = DateTime.UtcNow;
        character.FinesJson = JsonSerializer.Serialize(fines);
        await _dbContext.SaveChangesAsync();

        return Ok(new { success = true });
    }
}
