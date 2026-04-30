using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.DToS;
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
    public async Task<IActionResult> Create([FromBody] CharacterCreationDto dto)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        try
        {
            var newCharacter = await _characterService.CreateCharacterAsync(steamId, dto);
            // Re-fetch en DTO aplati pour que le jeu recoive immediatement les
            // morphs flat et ne passe pas par BrowDown=0/etc apres creation.
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

    /// <summary>
    /// PUT api/Character/{id}/appearance
    /// Remplace integralement le bloc d'apparence (gender, color, morphs, clothing,
    /// cheveux, barbe). Pas de patch partiel. Le createur, le coiffeur, le futur
    /// chir esthetique et toute autre source font tous un GET puis un PUT complet.
    /// C'est l'unique endpoint d'ecriture de l'apparence cote API.
    /// </summary>
    [HttpPut("{id}/appearance")]
    [Authorize]
    public async Task<IActionResult> SetAppearance(string id, [FromBody] CharacterAppearanceDto dto)
    {
        var steamId = GetSteamId();
        if (string.IsNullOrEmpty(steamId)) return Unauthorized();
        if (dto == null) return BadRequest(new { message = "Bloc d'apparence manquant." });

        var success = await _characterService.SetAppearanceAsync(steamId, id, dto);
        if (!success) return NotFound("Personnage introuvable ou vous n'etes pas le proprietaire.");

        // Retourne le character a jour (forme aplatie consommee directement par le jeu).
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

        // Retourne CharacterResponseDto (Character + morphs aplatis) pour matcher
        // exactement la classe CharacterApi cote jeu.
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

    /// <summary>
    /// GET api/Character/selected
    /// Retourne le personnage actuellement sélectionné par le joueur authentifié.
    /// Retourne 404 si aucun personnage n'est sélectionné.
    /// </summary>
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

    /// <summary>
    /// POST api/Character/{id}/select
    /// Définit le personnage actif du joueur. Désélectionne l'ancien au passage.
    /// </summary>
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