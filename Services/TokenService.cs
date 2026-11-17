using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using Microsoft.IdentityModel.Tokens;
using para.DTOs;
using para.Models;
using para.Security;

namespace para.Services;

public interface ITokenService
{
    AccessTokenDTO Issue(User user);
}

public sealed class TokenService(JwtSettings settings, IMapper mapper) : ITokenService
{
    public AccessTokenDTO Issue(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Login),
            new Claim(ApiPolicies.RoleClaim, user.Role!.RoleName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)
        };
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires, mapper.Map<UserProfileDTO>(user));
    }
}
