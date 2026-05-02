using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;
using System.Security.Claims;

namespace OpenFramework.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CharacterController : Controller
{
    private readonly CharacterService _characterService;

    public CharacterController(CharacterService characterService)
    {
        _characterService = characterService;
    }

    private string? GetSteamId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateCharacterRequest request)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        try
        {
            var newCharacter = await _characterService.CreateCharacterAsync(steamId, request);
            var response = await _characterService.GetByIdAsResponseAsync(newCharacter.Id);
            return CreatedAtAction(nameof(GetById), new { id = newCharacter.Id }, response);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "Erreur lors de la création", error = ex.Message });
        }
    }

    [HttpPost("{id}/changeActualJob")]
    [Authorize]
    public async Task<IActionResult> ChangeActualJob(string id, [FromQuery] string newJobIdent)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var character = await _characterService.GetByIdAsync(id);
        if (character == null) return NotFound();

        character.ActualJobIdent = newJobIdent;
        var success = await _characterService.UpdateCharacterAsync(steamId, id, character);
        if (!success) return NotFound("Personnage introuvable ou vous n'êtes pas le propriétaire.");

        return Ok(character);
    }

    [HttpPost("{id}/update")]
    [Authorize]
    public async Task<IActionResult> Update([FromBody] Character character, string id)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var success = await _characterService.UpdateCharacterAsync(steamId, id, character);
        if (!success) return NotFound("Personnage introuvable ou vous n'êtes pas le propriétaire.");

        return Ok(character);
    }

    [HttpPost("{id}/appearance/update")]
    [HttpPut("{id}/appearance")]
    [Authorize]
    public async Task<IActionResult> SetAppearance(string id, [FromBody] AppearanceBody body)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        if (body == null) return BadRequest(new { message = "Bloc d'apparence manquant." });

        var success = await _characterService.SetAppearanceAsync(steamId, id, body);
        if (!success) return NotFound("Personnage introuvable ou vous n'êtes pas le propriétaire.");

        var refreshed = await _characterService.GetByIdAsResponseAsync(id);
        return Ok(refreshed);
    }

    [HttpDelete("{id}/delete")]
    [Authorize]
    public async Task<IActionResult> Delete(string id)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var success = await _characterService.DeleteCharacterAsync(id, steamId);
        if (!success) return NotFound("Impossible de supprimer le personnage.");

        return NoContent();
    }

    [HttpGet("all")]
    [Authorize]
    public async Task<IActionResult> GetAllOwnedCharacters()
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var characters = await _characterService.GetAllByOwnerAsResponseAsync(steamId);
        return Ok(characters);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(string id)
    {
        var character = await _characterService.GetByIdAsResponseAsync(id);
        if (character == null) return NotFound();
        return Ok(character);
    }

    [HttpGet("selected")]
    [Authorize]
    public async Task<IActionResult> GetSelected()
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var character = await _characterService.GetSelectedCharacterAsResponseAsync(steamId);
        if (character == null) return NotFound(new { message = "Aucun personnage sélectionné." });
        return Ok(character);
    }

    [HttpPost("{id}/select")]
    [Authorize]
    public async Task<IActionResult> Select(string id)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();

        var success = await _characterService.SelectCharacterAsync(steamId, id);
        if (!success) return NotFound(new { message = "Personnage introuvable ou vous n'êtes pas le propriétaire." });

        return Ok(new { message = "Personnage sélectionné.", characterId = id });
    }
}
