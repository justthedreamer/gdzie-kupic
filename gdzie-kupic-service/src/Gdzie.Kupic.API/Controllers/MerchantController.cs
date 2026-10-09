namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Merchant))]
[Route("api/merchant")]
public class MerchantController(IMerchantService merchantService, ISubscriptionService subscriptionService) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<MerchantMeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var result = await merchantService.GetMeAsync(User.GetUserId(), ct);

        return result.IsSuccess ? Ok(ToResponse(result.Value!)) : ToProblem(result);
    }

    [HttpPost("onboarding")]
    [ProducesResponseType<MerchantMeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Onboard([FromBody] Onboarding.Request request, CancellationToken ct)
    {
        var branch = request.Branch is null
            ? null
            : new BranchInput(
                request.Branch.DisplayName,
                request.Branch.Phone,
                request.Branch.Website,
                request.Branch.Latitude,
                request.Branch.Longitude,
                request.Branch.Address);

        var result = await merchantService.OnboardAsync(
            User.GetUserId(), new OnboardingInput(request.Name, request.Description, branch), ct);

        return result.IsSuccess ? Created("/api/merchant/me", ToResponse(result.Value!)) : ToProblem(result);
    }

    [HttpGet("subscriptions")]
    [ProducesResponseType<IReadOnlyList<Subscriptions.Response>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListSubscriptions(CancellationToken ct)
    {
        var result = await subscriptionService.ListAsync(User.GetUserId(), ct);

        return result.IsSuccess
            ? Ok(result.Value!.Select(ToResponse).ToList())
            : ToProblem(result);
    }

    [HttpPost("subscriptions")]
    [ProducesResponseType<Subscriptions.Response>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSubscription([FromBody] Subscriptions.Request request, CancellationToken ct)
    {
        var result = await subscriptionService.AddAsync(User.GetUserId(), request.CategoryId, request.TagId, ct);

        return result.IsSuccess
            ? Created($"/api/merchant/subscriptions/{result.Value!.Id}", ToResponse(result.Value))
            : ToProblem(result);
    }

    [HttpDelete("subscriptions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveSubscription(Guid id, CancellationToken ct)
    {
        var result = await subscriptionService.RemoveAsync(User.GetUserId(), id, ct);

        return result.IsSuccess ? NoContent() : ToProblem(result);
    }

    private IActionResult ToProblem<T>(SubscriptionResult<T> result)
    {
        var (status, title) = result.Error switch
        {
            SubscriptionError.NotOnboarded => (StatusCodes.Status404NotFound, "Not onboarded"),
            SubscriptionError.NotFound => (StatusCodes.Status404NotFound, "Subscription not found"),
            SubscriptionError.Duplicate => (StatusCodes.Status409Conflict, "Duplicate subscription"),
            _ => (StatusCodes.Status400BadRequest, "Validation error"),
        };

        return Problem(statusCode: status, title: title, detail: result.Message);
    }

    private static Subscriptions.Response ToResponse(MerchantSubscription s) => new(s.Id, s.CategoryId, s.TagId);
    private IActionResult ToProblem<T>(MerchantResult<T> result)
    {
        var (status, title) = result.Error switch
        {
            MerchantError.NotOnboarded => (StatusCodes.Status404NotFound, "Not onboarded"),
            MerchantError.AlreadyOnboarded => (StatusCodes.Status409Conflict, "Already onboarded"),
            MerchantError.GeocodingFailed => (StatusCodes.Status502BadGateway, "Geocoding failed"),
            _ => (StatusCodes.Status400BadRequest, "Validation error"),
        };

        return Problem(statusCode: status, title: title, detail: result.Message);
    }

    private static MerchantMeResponse ToResponse(Merchant merchant)
    {
        var b = merchant.Branches.First();

        return new MerchantMeResponse(
            merchant.Id,
            merchant.Name,
            merchant.Description,
            new BranchResponse(b.Id, b.DisplayName, b.Coordinates.Latitude, b.Coordinates.Longitude, b.AddressDisplayName, b.Phone, b.Website));
    }
}