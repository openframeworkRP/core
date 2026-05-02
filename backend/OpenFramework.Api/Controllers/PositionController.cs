using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using System.Security.Claims;

namespace OpenFramework.Api.Controllers;

[Route("api/characters/{id}/positions")]
[ApiController]
public class PositionController : ControllerBase
{
    private readonly OpenFrameworkDbContext _context;

    public PositionController(OpenFrameworkDbContext context)
    {
        _context = context;
    }

    [HttpGet("lastposition")]
    [Authorize]
    public async Task<IActionResult> Get(string id)
    {
        var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var exists = await _context.Characters.AsNoTracking().AnyAsync(t => t.Id == id && t.OwnerId == steamId);
        if (!exists)
            return NotFound("Personnage introuvable ou vous n'avez pas l'autorisation.");

        var lastPosition = await _context.CharacterPositions.AsNoTracking().FirstOrDefaultAsync(t => t.CharacterId == id);
        if (lastPosition == null)
            return NotFound("Aucune position enregistrée pour ce personnage.");

        return Ok(lastPosition);
    }

    [HttpPost("update")]
    [Authorize]
    public async Task<IActionResult> Update(string id, [FromBody] PositionUpdateRequest request)
    {
        var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var exists = await _context.Characters.AsNoTracking().AnyAsync(t => t.Id == id && t.OwnerId == steamId);
        if (!exists)
            return Unauthorized("Action interdite ou personnage inexistant.");

        var currentPosition = await _context.CharacterPositions.FirstOrDefaultAsync(t => t.CharacterId == id);
        if (currentPosition == null)
        {
            currentPosition = new CharacterPosition { CharacterId = id };
            _context.CharacterPositions.Add(currentPosition);
        }

        currentPosition.X = request.X;
        currentPosition.Y = request.Y;
        currentPosition.Z = request.Z;

        await _context.SaveChangesAsync();
        return Ok(currentPosition);
    }
}
