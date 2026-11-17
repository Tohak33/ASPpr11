using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using para.Models;
using para.Repositories;

namespace para.Security;

public static class ApiSecurityRegistration
{
    public static void AddApiSecurity(this IServiceCollection services, IConfiguration config, IWebHostEnvironment environment)
    {
        var jwt = ReadJwtSettings(config, environment);
        services.AddSingleton(jwt);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<IAuthorizationHandler, ProfileOwnerHandler>();

        services.AddAuthentication().AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwt.SigningKey,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,
                IgnoreTrailingSlashWhenValidatingAudience = false,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = JwtRegisteredClaimNames.UniqueName,
                RoleClaimType = ApiPolicies.RoleClaim
            };
            options.Events = new JwtBearerEvents { OnTokenValidated = ValidateAccountAsync };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(ApiPolicies.SignedIn, policy =>
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
            options.AddPolicy(ApiPolicies.Admin, policy =>
                policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser().RequireRole("Admin"));
            options.AddPolicy(ApiPolicies.ProfileOwner, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new ProfileOwnerRequirement()));
        });

        var origin = config["Cors:FrontendOrigin"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
            originUri.Scheme is not ("http" or "https") || origin != originUri.GetLeftPart(UriPartial.Authority))
            throw new InvalidOperationException("Cors:FrontendOrigin должен быть точным origin без пути и завершающего слеша.");

        services.AddCors(options => options.AddPolicy(ApiPolicies.FrontendCors, policy => policy
            .WithOrigins(origin).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("X-Total-Count", "Retry-After")));

        var permitLimit = config.GetValue("LoginRateLimit:PermitLimit", 5);
        var windowSeconds = config.GetValue("LoginRateLimit:WindowSeconds", 60);
        if (permitLimit <= 0 || windowSeconds <= 0)
            throw new InvalidOperationException("Настройки ограничения входа должны быть положительными.");

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ApiPolicies.LoginLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Слишком много попыток входа. Повторите позже."
                }, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken);
            };
        });
    }

    private static JwtSettings ReadJwtSettings(IConfiguration config, IWebHostEnvironment environment)
    {
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];
        var minutes = config.GetValue("Jwt:AccessTokenMinutes", 30);
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || minutes is < 1 or > 1440)
            throw new InvalidOperationException("Проверьте настройки издателя, аудитории и времени жизни JWT.");

        var configuredKey = config["Jwt:SigningKey"];
        var key = string.IsNullOrWhiteSpace(configuredKey) && environment.IsDevelopment()
            ? RandomNumberGenerator.GetBytes(32)
            : string.IsNullOrWhiteSpace(configuredKey) ? [] : Convert.FromBase64String(configuredKey);
        if (key.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey должен содержать минимум 32 байта в Base64.");
        return new JwtSettings(issuer, audience, minutes, new SymmetricSecurityKey(key));
    }

    private static async Task ValidateAccountAsync(TokenValidatedContext context)
    {
        if (ApiPolicies.UserId(context.Principal) is not int id)
        {
            context.Fail("Invalid subject.");
            return;
        }
        var users = context.HttpContext.RequestServices.GetRequiredService<IRepository<User>>();
        var user = await users.GetByIdAsync(id, context.HttpContext.RequestAborted);
        if (user is not { IsActive: true, Role: not null } || !context.Principal!.IsInRole(user.Role.RoleName))
            context.Fail("Account is inactive or the role has changed.");
    }
}
