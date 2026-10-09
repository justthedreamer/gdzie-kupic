using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Gdzie.Kupic.Service.API;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? throw new InvalidOperationException("Authenticated principal has no 'sub' claim."));
}