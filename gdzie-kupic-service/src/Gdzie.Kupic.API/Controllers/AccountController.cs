namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Service.API.Contract.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize]
[Route("api/account")]
public class AccountController(IAuthService authService) : ControllerBase
{
    [HttpGet("profile")]
    [ProducesResponseType<Profile.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var profile = await authService.GetProfileAsync(User.GetUserId());

        return profile is null ? NotFoundProblem() : Ok(ToResponse(profile));
    }

    [HttpPut("profile")]
    [ProducesResponseType<Profile.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] Profile.UpdateRequest request)
    {
        var result = await authService.UpdateFirstNameAsync(User.GetUserId(), request.FirstName);

        if (result.NotFound) return NotFoundProblem();

        if (result.ValidationError is not null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: result.ValidationError);
        }

        return Ok(ToResponse(result.Profile!));
    }

    private ObjectResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Account not found.");

    private static Profile.Response ToResponse(ProfileResult p) => new(p.Email, p.FirstName, p.Role.ToString());
}
