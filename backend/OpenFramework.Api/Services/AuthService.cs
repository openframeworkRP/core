using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenFramework.Api.Data;
using OpenFramework.Api.Contracts;
using OpenFramework.Api.Models;

namespace OpenFramework.Api.Services;

public class AuthService
{
    private readonly OpenFrameworkDbContext _db;

    public AuthService(OpenFrameworkDbContext db)
    {
        _db = db;
    }
    
    public string GenerateJwtToken(string steamId, string secretKey)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, steamId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "OpenFrameworkApi",
            audience: "OpenFrameworkPlayers",
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    
    public async Task<bool> EnsureUserExistsAsync(long steamId)
    {
        var id = steamId.ToString();
        if (_db.Users.Any(u => u.Id == id)) return true;
        _db.Users.Add(new User { Id = id });
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ValidateTokenWithFacePunchService( long steamId, string token )
    {
        var http = new System.Net.Http.HttpClient();
        var data = new Dictionary<string, object>
        {
            { "steamid", steamId },
            { "token", token }
        };
        var content = new StringContent( JsonSerializer.Serialize( data ), Encoding.UTF8, "application/json" );
        var result = await http.PostAsync( "https://services.facepunch.com/sbox/auth/token", content );

        if ( result.StatusCode != HttpStatusCode.OK ) return false;
	
        var response = await result.Content.ReadFromJsonAsync<Contracts.TokenValidationResult>();
        if ( response is null || response.Status != "ok" ) return false;
        var userExist = _db.Users.FirstOrDefault(t => t.Id == response.SteamId.ToString());
        if (userExist is null)
        {
            _db.Users.Add(new User()
            {
                Id = response.SteamId.ToString(),
            });
            _db.SaveChanges();
        }
        return response.SteamId == steamId;
    }
    
    public string GenerateServerJwtToken(string jwtSecret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
 
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "gameserver"),
            new Claim(ClaimTypes.Role, "GameServer"),
            // Pas d'expiration courte — ou ajuste selon ta politique de sécurité
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
 
        var token = new JwtSecurityToken(
            issuer: "OpenFrameworkApi",
            audience: "OpenFrameworkPlayers",
            claims: claims,
            // Token valide 30 jours — le serveur re-login si nécessaire
            expires: DateTime.UtcNow.AddDays(30),
            signingCredentials: creds
        );
 
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}