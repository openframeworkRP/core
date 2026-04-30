using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Data;
using OpenFramework.Api.DToS;
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
    public IActionResult Get(string id)
    {
        var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var character = _context.Characters.FirstOrDefault(t => t.Id == id && t.OwnerId == steamId);
        
        if (character == null)
        {
            return NotFound("Personnage introuvable ou vous n'avez pas l'autorisation.");
        }

        var lastPosition = _context.CharacterPositions.FirstOrDefault(t => t.CharacterId == id);
        
        if (lastPosition == null)
        {
            return NotFound("Aucune position enregistrée pour ce personnage.");
        }

        return Ok(lastPosition);
    }
    
    [HttpPost("update")]
    public IActionResult Update(string id, [FromBody] CharacterPositionUpdateDto request)
    {
        var steamId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        var characterExists = _context.Characters.Any(t => t.Id == id && t.OwnerId == steamId);
        if (!characterExists)
        {
            return Unauthorized("Action interdite ou personnage inexistant.");
        }
        
        var currentPosition = _context.CharacterPositions.FirstOrDefault(t => t.CharacterId == id);
        
        if (currentPosition == null)
        {
            currentPosition = new CharacterPosition { CharacterId = id };
            _context.CharacterPositions.Add(currentPosition);
        }
        
        currentPosition.X = request.X;
        currentPosition.Y = request.Y;
        currentPosition.Z = request.Z;

        _context.SaveChanges();
        
        return Ok(currentPosition);
    }
}