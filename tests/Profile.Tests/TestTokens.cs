using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using Profile.Tests.Auth;

namespace Profile.Tests;

/// <summary>
/// Mints RS256 access tokens the way auth-svc does (issuer/audience "job-platform",
/// NameIdentifier as the subject), so tests exercise the same claim path as production.
/// </summary>
internal static class TestTokens
{
    public static string Create(string userId, string role = "User")
    {
        var key = new RsaSecurityKey(TestSigningKeys.Current) { KeyId = TestSigningKeys.Kid };
        var token = new JwtSecurityToken(
            issuer: "job-platform",
            audience: "job-platform",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
            ],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
