namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Notifications;
using Gdzie.Kupic.Service.API.Contract.Push;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = $"{nameof(Role.Buyer)},{nameof(Role.Merchant)}")]
[Route("api/push")]
public class PushController(IPushSubscriptionService push) : ControllerBase
{
    [HttpGet("vapid-public-key")]
    [ProducesResponseType<Push.VapidKeyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetVapidPublicKey()
    {
        var key = push.GetVapidPublicKey();
        if (key is not null) return Ok(new Push.VapidKeyResponse(key));

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Web Push is not configured",
            Detail = "The server has no VAPID configuration.",
        };
        problem.Extensions["code"] = "push_not_configured";

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }

    [HttpPut("subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Subscribe([FromBody] Push.SubscribeRequest request, CancellationToken ct)
    {
        var result = await push.RegisterAsync(User.GetUserId(), request.Endpoint, request.Keys?.P256dh, request.Keys?.Auth, ct);

        return result.IsSuccess ? NoContent() : Invalid(result);
    }

    [HttpDelete("subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unsubscribe([FromBody] Push.UnsubscribeRequest request, CancellationToken ct)
    {
        var result = await push.UnregisterAsync(User.GetUserId(), request.Endpoint, ct);

        return result.IsSuccess ? NoContent() : Invalid(result);
    }

    private ObjectResult Invalid(PushResult result) =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: result.Message);
}
