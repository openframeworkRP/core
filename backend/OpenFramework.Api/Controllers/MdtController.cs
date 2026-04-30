using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Data;
using OpenFramework.Api.DToS;
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

    #region Casier judiciaire

    [HttpGet("criminalrecord/{characterId}")]
    public async Task<IActionResult> GetCriminalRecords(string characterId)
    {
        var criminalsRecords = _dbContext.CriminalRecords.Where(t => t.ToWhoCharacterId == characterId).ToList();

        if (criminalsRecords.Any())
        {
            return Ok(new
            {
                sucess = true,
                message = "l'individue n'a pas de casier judiciaire."
            });
        }

        return Ok(new
            {
                sucess = true,
                criminalsRecords
            }
        );
    }
    
    [HttpPost("criminalrecord/{characterId}/addrecord")]
    public async Task<IActionResult> AddRecordInCriminalRecord(string characterId, [FromBody] AddCriminalRecordDto dto)
    {
        var record = new CriminalRecord()
        {
            Id = Guid.NewGuid().ToString(),
            ToWhoCharacterId = characterId,
            FromWhoMandatedId = dto.FromWhoMandatedId,
            Description = dto.Description,
            Title = dto.Title,
        };
        
        _dbContext.Add(record);
        await _dbContext.SaveChangesAsync();
        return Ok(new
        {
            sucess = true,
            record,
        });
    }
    
    [HttpPost("criminalrecord/{characterId}/removerecord")]
    public Task<IActionResult> RemoveRecordInCriminalRecord(string characterId)
    {
        throw new NotImplementedException("à faire");
    }
    #endregion

    #region Vehicule

    

    #endregion

    #region Mandat

    

    #endregion

    #region Garde à vue

    

    #endregion

    #region Licence d'arme

    

    #endregion

    #region MyRegion

    

    #endregion
}