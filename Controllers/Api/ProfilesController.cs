using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using para.DTOs;
using para.Security;
using para.Services;

namespace para.Controllers.Api;

[ApiController]
[Authorize(Policy = ApiPolicies.SignedIn)]
[Route("api/profiles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfilesController(IProfileService profiles, IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserProfileDTO>(StatusCodes.Status200OK)]
    public Task<ActionResult<UserProfileDTO>> GetMe(CancellationToken ct) =>
        GetById(ApiPolicies.UserId(User)!.Value, ct);

    [HttpGet("{id:int}")]
    [ProducesResponseType<UserProfileDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDTO>> GetById(int id, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(id, ct);
        if (profile == null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, profile, ApiPolicies.ProfileOwner)).Succeeded)
            return Forbid(JwtBearerDefaults.AuthenticationScheme);
        return Ok(profile);
    }

    [HttpPatch("{id:int}")]
    [ProducesResponseType<UserProfileDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDTO>> Update(int id, UpdateProfileDTO input, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(id, ct);
        if (profile == null) return NotFound();
        if (!(await authorization.AuthorizeAsync(User, profile, ApiPolicies.ProfileOwner)).Succeeded)
            return Forbid(JwtBearerDefaults.AuthenticationScheme);
        var updated = await profiles.UpdateAsync(id, input, ct);
        return updated == null ? NotFound() : Ok(updated);
    }
}
