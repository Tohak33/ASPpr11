using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace para.Security;

public static class ApiPolicies
{
    public const string SignedIn = "ApiSignedIn";
    public const string Admin = "ApiAdmin";
    public const string ProfileOwner = "ApiProfileOwner";
    public const string FrontendCors = "FrontendCors";
    public const string LoginLimit = "ApiLoginLimit";
    public const string RoleClaim = "role";

    public static int? UserId(ClaimsPrincipal? principal) =>
        int.TryParse(principal?.FindFirstValue(JwtRegisteredClaimNames.Sub), NumberStyles.None,
            CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
