using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Controllers;
[Route("api/characters/{id}/cloths")]
public class ClothesController : Controller
{
    private readonly OpenFrameworkDbContext _context;
    private string? GetSteamId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public ClothesController(OpenFrameworkDbContext context)
    {
        _context = context;
    }
    
    [HttpGet()]
    [Authorize]
    public IActionResult Get()
    {
        var steamId = GetSteamId();
        if (steamId == null)
        {
            return Unauthorized("Impossible de récupérer le steam id dans la clée... ( dada fait un effort stp 😊😊) ");
        }
        
        var cloths = _context.Cloths.Where(t => t.OwnerId == steamId)
            .ToList();
        return Ok(cloths);
    }
    
    

    [HttpPost("update")]
    [Authorize]
    public IActionResult Update(List<Cloth> newCloths)
    {
        var steamId = GetSteamId();
        if (steamId == null)
        {
            return Unauthorized("Impossible de récupérer le steam id dans la clée... ( dada fait un effort stp 😊😊) ");
        }

        _context.Cloths.Where(t => t.OwnerId == steamId)
            .ExecuteDelete();
        
        _context.Cloths.AddRange(newCloths);
        
        _context.SaveChanges();
        
        return Ok("Parfaitement update.");
    }

    [HttpDelete("clear")]
    public IActionResult Delete()
    {
        var steamId = GetSteamId();
        if (steamId == null)
        {
            return Unauthorized("Impossible de récupérer le steam id dans la clée... ( dada fait un effort stp 😊😊) ");
        }

        _context.Cloths.Where(t => t.OwnerId == steamId)
            .ExecuteDelete();
        
        _context.SaveChanges();
        return Ok("Parfaitement delete.");
    }
}