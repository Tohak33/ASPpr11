using Microsoft.IdentityModel.Tokens;

namespace para.Security;

public sealed record JwtSettings(string Issuer, string Audience, int AccessTokenMinutes, SymmetricSecurityKey SigningKey);
