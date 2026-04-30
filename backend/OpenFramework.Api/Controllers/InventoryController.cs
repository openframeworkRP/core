using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Data;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;
[Route("api/characters/actual/inventory")]
public class InventoryController : Controller
{
    
    private readonly CharacterService  _characterService;
    private readonly InventoryService  _inventoryService;

    public InventoryController(CharacterService  characterService,
        InventoryService  inventoryService)
    {
        _characterService = characterService;
        _inventoryService = inventoryService;
    }
    
    private string? GetSteamId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    [HttpPost("add")]
    [Authorize]
    public async Task<IActionResult> AddInInventoryItem([FromBody] InventoryItem item)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        
        var character = await _characterService.GetSelectedCharacterAsync(steamId);
        if (character == null) return NotFound("pas de joueur sélectionné");
        
        _inventoryService.AddItemInActualInventory(character.Id, item);
        return Ok();
    }

    [HttpGet("get")]
    [Authorize]
    public  async Task<IActionResult> Get()
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        
        var character = await _characterService.GetSelectedCharacterAsync(steamId);
        if (character == null) return NotFound("pas de joueur sélectionné");
        
        var items = _inventoryService.GetAllItems(character.Id);
        return Ok(items);
    }

    [HttpPost("delete")]
    [Authorize]
    public async Task<IActionResult> DeleteInInventory(InventoryItem item)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        
        var character = await _characterService.GetSelectedCharacterAsync(steamId);
        if (character == null) return NotFound("pas de joueur sélectionné");
        
        _inventoryService.DeleteItemInActualInventory(character.Id, item.Line, item.Collum);
        return Ok();
    }

    [HttpPost("clear")]
    [Authorize]
    public async Task<IActionResult> ClearInventory()
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        
        var character = await _characterService.GetSelectedCharacterAsync(steamId);
        if (character == null) return NotFound("pas de joueur sélectionné");
        
        _inventoryService.ClearInventory(character.Id);
        return Ok();
    }
    
    
}