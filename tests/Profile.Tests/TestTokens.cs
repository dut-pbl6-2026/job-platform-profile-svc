using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Profile.Tests;

/// <summary>
/// Mints HS256 access tokens the way auth-svc does (issuer/audience "job-platform",
/// NameIdentifier as the subject), so tests exercise the same claim path as production.
/// </summary>
internal static class TestTokens
{
    public static string Create(string userId, string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ProfileApiFixture.JwtSecret));
        var token = new JwtSecurityToken(
            issuer: "job-platform",
            audience: "job-platform",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
