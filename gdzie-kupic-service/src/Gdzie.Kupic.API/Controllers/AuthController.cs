using System.Net;
using System.Security.Claims;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Services;
using Gdzie.Kupic.Service.API.Contract.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gdzie.Kupic.Service.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IDomainMapper domainMapper,
    IAuthService authService,
    IOptions<GoogleAuthSettings> googleAuthOptions) : ControllerBase
{
    private const string RoleAuthPropertyKey = "role";
    [HttpPost("sign-in")]
    public async Task<IActionResult> SingIn([FromBody] SignIn.Request request)
    {
        var result = await authService.SignInAsync(request.Email, request.Password);

        if (result.InvalidCredentialsError is not null)
        {
            return Problem(
                detail: "Invalid username or password.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        return Ok(new SignIn.Response(result.AccessToken, result.RefreshToken, result.ExpiresAt));
    }

    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUp([FromBody] SignUp.Request request)
    {
        var (role, invalidRoleError) = domainMapper.MapRole(request.Role);

        if (invalidRoleError is not null)
        {
            // TODO: Rewrite problem message.
            return Problem(
                detail:
                "Something went wrong during sign up attempt on platform site. Please contact the site administration.",
                statusCode: (int)HttpStatusCode.InternalServerError);
        }

        if (role is Role.Admin)
        {
            return Problem(
                detail: "Cannot sign up as admin.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        var result = await authService.SignUpAsync(request.Email, request.Password, role);

        if (result.ValidationError is not null)
        {
            return Problem(
                detail: result.ValidationError,
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        if (result.EmailAlreadyExistsError is not null)
        {
            return Problem(
                detail: "Email already exists.",
                statusCode: (int)HttpStatusCode.Conflict);
        }

        return Ok(new SignUp.Response(result.AccessToken, result.RefreshToken, result.ExpiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] Refresh.Request request)
    {
        var result = await authService.RefreshAsync(request.RefreshToken);

        if (result.InvalidRefreshTokenError is not null)
        {
            return Problem(
                detail: "Invalid or expired refresh token.",
                statusCode: (int)HttpStatusCode.Unauthorized);
        }

        return Ok(new Refresh.Response(result.AccessToken, result.RefreshToken, result.ExpiresAt));
    }

    [HttpGet("google/login")]
    public IActionResult GoogleLogin([FromQuery] string role)
    {
        var (mappedRole, invalidRoleError) = domainMapper.MapRole(role);

        if (invalidRoleError is not null || mappedRole is Role.Admin)
        {
            return Problem(
                detail: "Role must be either 'Buyer' or 'Merchant'.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback)),
        };
        properties.Items[RoleAuthPropertyKey] = mappedRole.ToString();

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleAuthConstants.ExternalCookieScheme);

        if (!authenticateResult.Succeeded
            || authenticateResult.Principal is null
            || authenticateResult.Properties is null
            || !authenticateResult.Properties.Items.TryGetValue(RoleAuthPropertyKey, out var roleValue)
            || !Enum.TryParse<Role>(roleValue, out var role))
        {
            return Problem(
                detail: "Google sign-in failed.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        await HttpContext.SignOutAsync(GoogleAuthConstants.ExternalCookieScheme);

        var providerKey = authenticateResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = authenticateResult.Principal.FindFirstValue(ClaimTypes.Email);

        if (providerKey is null || email is null)
        {
            return Problem(
                detail: "Google account did not provide the required profile information.",
                statusCode: (int)HttpStatusCode.BadRequest);
        }

        var result = await authService.GoogleSignInAsync(providerKey, email, role);

        var callbackUrl = googleAuthOptions.Value.FrontendCallbackUrl
            + $"#access_token={Uri.EscapeDataString(result.AccessToken)}"
            + $"&refresh_token={Uri.EscapeDataString(result.RefreshToken)}"
            + $"&expires_at={Uri.EscapeDataString(result.ExpiresAt.ToString("O"))}";

        return Redirect(callbackUrl);
    }
}