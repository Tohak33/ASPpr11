using Microsoft.AspNetCore.Authorization;
using para.DTOs;

namespace para.Security;

public sealed class ProfileOwnerRequirement : IAuthorizationRequirement;

public sealed class ProfileOwnerHandler : AuthorizationHandler<ProfileOwnerRequirement, UserProfileDTO>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ProfileOwnerRequirement requirement, UserProfileDTO resource)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            (context.User.IsInRole("Admin") || ApiPolicies.UserId(context.User) == resource.Id))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
