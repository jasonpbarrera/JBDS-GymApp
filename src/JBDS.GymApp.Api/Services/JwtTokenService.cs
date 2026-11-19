using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JBDS.GymApp.Api.Data;
using JBDS.Platform.Core.Domain;
using JBDS.Platform.Core.Security;
using Microsoft.IdentityModel.Tokens;

namespace JBDS.GymApp.Api.Services;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public (string Token, DateTime ExpiresAtUtc) GenerateToken(
        ApplicationUser user,
        IList<string> roles,
        Tenant tenant,
        Product product)
    {
        var key = configuration["Jwt:Key"]
                  ?? "JBDS_GYMAPP_DEV_SIGNING_KEY_CHANGE_IN_PRODUCTION_2026_MIN_32_CHARS!";
        var issuer = configuration["Jwt:Issuer"] ?? "JBDS.Platform";
        var audience = configuration["Jwt:Audience"] ?? "JBDS.GymApp";
        var expiresAtUtc = DateTime.UtcNow.AddHours(12);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.FullName),
            new(PlatformClaimTypes.TenantId, tenant.Id.ToString()),
            new(PlatformClaimTypes.TenantSlug, tenant.Slug),
            new(PlatformClaimTypes.ProductId, product.Id.ToString()),
            new(PlatformClaimTypes.ProductKey, product.Key)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
