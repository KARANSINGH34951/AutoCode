using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Enrichly.JobAutomation.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Enrichly.JobAutomation.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string Create(User user)
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email) };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
