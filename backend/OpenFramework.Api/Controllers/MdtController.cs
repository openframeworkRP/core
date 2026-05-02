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
}
