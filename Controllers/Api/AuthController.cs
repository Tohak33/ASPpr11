using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using para.DTOs;
using para.Security;
using para.Services;

namespace para.Controllers.Api;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(IAccountService accounts, ITokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<AccessTokenDTO>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccessTokenDTO>> Register(RegisterDTO input, CancellationToken ct)
    {
        var login = input.Login.Trim();
        if (login.Length < 3) return BadRequest(new ProblemDetails { Status = 400, Title = "Логин должен содержать не менее 3 символов." });
        var user = await accounts.RegisterAsync(login, input.Password, input.FullName.Trim(), ct);
        return CreatedAtAction(nameof(ProfilesController.GetById), "Profiles", new { id = user.UserID }, tokens.Issue(user));
    }

    [HttpPost("login")]
    [EnableRateLimiting(ApiPolicies.LoginLimit)]
    [ProducesResponseType<AccessTokenDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccessTokenDTO>> Login(LoginDTO input, CancellationToken ct)
    {
        var user = await accounts.LoginAsync(input.Login.Trim(), input.Password, ct);
        return user?.Role is null
            ? Unauthorized(new ProblemDetails { Status = 401, Title = "Неверный логин или пароль." })
            : Ok(tokens.Issue(user));
    }
}
