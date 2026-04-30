using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.DToS;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

[ApiController]
[Route("api/admin/character")]
[Authorize(Roles = "GameServer")]
public class AdminCharacterController : ControllerBase
{
    private readonly CharacterService _characterService;

    public AdminCharacterController(CharacterService characterService)
    {
        _characterService = characterService;
    }

    /// <summary>
    /// Met à jour des champs autorisés (prénom / nom) d'un personnage en
    /// override admin (sans contrôle de propriétaire). Utilisé pour corriger
    /// les noms RP troll depuis le panel web.
    /// Requiert un JWT GameServer.
    /// </summary>
    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateCharacter(string id, [FromBody] AdminUpdateCharacterDto dto)
    {
        if (dto == null || (string.IsNullOrWhiteSpace(dto.FirstName) && string.IsNullOrWhiteSpace(dto.LastName)))
            return BadRequest(new { success = false, error = "Au moins FirstName ou LastName doit être fourni." });

        var firstName = dto.FirstName?.Trim();
        var lastName  = dto.LastName?.Trim();

        if (firstName != null && (firstName.Length == 0 || firstName.Length > 64))
            return BadRequest(new { success = false, error = "FirstName doit faire 1-64 caractères." });
        if (lastName != null && (lastName.Length == 0 || lastName.Length > 64))
            return BadRequest(new { success = false, error = "LastName doit faire 1-64 caractères." });

        var updated = await _characterService.AdminUpdateCharacterAsync(id, firstName, lastName);
        if (updated == null)
            return NotFound(new { success = false, error = "Personnage introuvable." });

        return Ok(new { success = true, character = updated });
    }

    /// <summary>
    /// Supprime définitivement un personnage en override admin (sans contrôle
    /// de propriétaire). Cas d'usage : nom RP troll / inapproprié signalé.
    /// Requiert un JWT GameServer.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCharacter(string id)
    {
        var ok = await _characterService.AdminDeleteCharacterAsync(id);
        if (!ok) return NotFound(new { success = false, error = "Personnage introuvable." });
        return Ok(new { success = true });
    }
}
