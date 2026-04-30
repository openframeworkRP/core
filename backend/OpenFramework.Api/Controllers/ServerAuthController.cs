using Microsoft.AspNetCore.Mvc;
using OpenFramework.Api.DToS;
using OpenFramework.Api.DTOs;
using OpenFramework.Api.Models;
using OpenFramework.Api.Services;

namespace OpenFramework.Api.Controllers;

[Route("api/auth")]
public class ServerAuthController : Controller
{
    private readonly AuthService _authService;
    private readonly IConfiguration _config;
    public ServerAuthController(AuthService authService
    , IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] TokenAccessorRequest request)
    { 
        if (!long.TryParse(request.Id, out long steamId))
            return BadRequest("Invalid SteamID format.");
        bool isValid = await _authService.ValidateTokenWithFacePunchService(steamId, request.Token);
        if (!isValid)
            return Unauthorized("Facepunch authentication failed.");
        var jwtSecret = _config["Jwt:Key"] ?? "Une_Cle_Tres_Longue_Et_Securisee_De_32_Chars";
        var token = _authService.GenerateJwtToken(request.Id, jwtSecret);

        return Ok(new { access_token = token });
    }
    
    [HttpPost("server-login")]
    public IActionResult ServerLogin([FromBody] ServerLoginDto request)
    {
        var expectedSecret = _config["Server:Secret"]
                             ?? throw new InvalidOperationException("Server:Secret manquant dans appsettings.");
 
        if (request.ServerSecret != expectedSecret)
            return Unauthorized("Secret serveur invalide.");
 
        var jwtSecret = _config["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key manquant.");
        var token = _authService.GenerateServerJwtToken(jwtSecret);
 
        return Ok(new { access_token = token });
    }
}