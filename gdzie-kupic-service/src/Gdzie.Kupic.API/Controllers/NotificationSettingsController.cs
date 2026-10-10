namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = $"{nameof(Role.Buyer)},{nameof(Role.Merchant)}")]
[Route("api/account/notification-settings")]
public class NotificationSettingsController(INotificationSettingsService settings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NotificationSettings.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get()
    {
        var enabled = await settings.GetEmailEnabledAsync(User.GetUserId());

        return enabled is null ? NotFoundProblem() : Ok(new NotificationSettings.Response(enabled.Value));
    }

    [HttpPut]
    [ProducesResponseType<NotificationSettings.Response>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromBody] NotificationSettings.UpdateRequest request) =>
        await settings.SetEmailEnabledAsync(User.GetUserId(), request.EmailEnabled)
            ? Ok(new NotificationSettings.Response(request.EmailEnabled))
            : NotFoundProblem();

    private ObjectResult NotFoundProblem() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: "Account not found.");
}
