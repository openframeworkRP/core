using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.Contracts;
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

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateCharacter(string id, [FromBody] AdminPatchCharacterRequest request)
    {
        if (request == null || (string.IsNullOrWhiteSpace(request.FirstName) && string.IsNullOrWhiteSpace(request.LastName)))
            return BadRequest(new { success = false, error = "Au moins FirstName ou LastName doit être fourni." });

        var firstName = request.FirstName?.Trim();
        var lastName  = request.LastName?.Trim();

        if (firstName != null && (firstName.Length == 0 || firstName.Length > 64))
            return BadRequest(new { success = false, error = "FirstName doit faire 1-64 caractères." });
        if (lastName != null && (lastName.Length == 0 || lastName.Length > 64))
            return BadRequest(new { success = false, error = "LastName doit faire 1-64 caractères." });

        var updated = await _characterService.AdminUpdateCharacterAsync(id, firstName, lastName);
        if (updated == null)
            return NotFound(new { success = false, error = "Personnage introuvable." });

        return Ok(new { success = true, character = updated });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCharacter(string id)
    {
        var ok = await _characterService.AdminDeleteCharacterAsync(id);
        if (!ok) return NotFound(new { success = false, error = "Personnage introuvable." });
        return Ok(new { success = true });
    }
}
