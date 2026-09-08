using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using ShoppyApp.Models;
using ShoppyApp.Settings;

namespace ShoppyApp.Services;

public class TokenService
{
    private readonly JwtSettings _settings;
    public TokenService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
       
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
       
        var token = new JwtSecurityToken(_settings.Issuer, _settings.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpiresMinutes), signingCredentials: credentials);
       
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
