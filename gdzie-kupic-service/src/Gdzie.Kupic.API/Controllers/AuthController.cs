using System.Net;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Services;
using Gdzie.Kupic.Service.API.Contract.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Gdzie.Kupic.Service.API.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IDomainMapper domainMapper, IAuthService authService) : ControllerBase
{
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
}