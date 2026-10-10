namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Merchant))]
[Route("api/merchant/feed")]
public class MerchantFeedController(IMerchantResponseService responseService, IMerchantFeedService feedService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Feed.FeedPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetFeed(
        [FromQuery] string? tab,
        [FromQuery] Guid? categoryId,
        [FromQuery] double? maxDistanceKm,
        [FromQuery] string? sort,
        [FromQuery] string? cursor,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        FeedTab? parsedTab = (tab?.ToLowerInvariant()) switch
        {
            null or "new" => FeedTab.New,
            "responded" => FeedTab.Responded,
            "all" => FeedTab.All,
            _ => null,
        };
        FeedSort? parsedSort = (sort?.ToLowerInvariant()) switch
        {
            null or "newest" => FeedSort.Newest,
            "nearest" => FeedSort.Nearest,
            _ => null,
        };

        if (parsedTab is null) return Invalid("Tab must be 'new', 'responded' or 'all'.");
        if (parsedSort is null) return Invalid("Sort must be 'newest' or 'nearest'.");

        var result = await feedService.GetFeedAsync(
            User.GetUserId(),
            new FeedRequest(parsedTab.Value, categoryId, maxDistanceKm, parsedSort.Value, cursor, limit),
            ct);

        return result.IsSuccess
            ? Ok(new Feed.FeedPage(result.Value!.Items.Select(FeedMapping.ToItem).ToList(), result.Value.NextCursor))
            : ToProblem(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType<Feed.Summary>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var result = await feedService.GetSummaryAsync(User.GetUserId(), ct);

        return result.IsSuccess
            ? Ok(new Feed.Summary(result.Value!.NewCount, result.Value.RespondedCount))
            : ToProblem(result);
    }

    [HttpGet("{postId:guid}")]
    [ProducesResponseType<Feed.FeedDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid postId, CancellationToken ct)
    {
        var result = await feedService.GetEntryAsync(User.GetUserId(), postId, ct);

        return result.IsSuccess ? Ok(FeedMapping.ToDetail(result.Value!)) : ToProblem(result);
    }

    [HttpPut("{postId:guid}/response")]    [ProducesResponseType<Feed.RespondResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Respond(Guid postId, [FromBody] Feed.RespondRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<ResponseState>(request.State, ignoreCase: false, out var state) ||
            !Enum.IsDefined(state))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation error",
                detail: "State must be one of CantHelp, MayHaveIt, HaveIt, CanOrderIt.");
        }

        var result = await responseService.RespondAsync(User.GetUserId(), postId, state, ct);

        return result.Error switch
        {
            ResponseError.None => Ok(new Feed.RespondResponse(result.State!.Value.ToString(), result.ThreadId, result.UpdatedAt!.Value)),
            ResponseError.PostNotActive => Conflict("post_not_active", "The post is no longer active."),
            _ => Problem(statusCode: StatusCodes.Status404NotFound, title: "Post not found"),
        };
    }

    private IActionResult Invalid(string detail) =>
        Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: detail);

    private IActionResult ToProblem<T>(FeedResult<T> result) => result.Error == FeedError.NotFound
        ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Post not found")
        : Invalid(result.Message ?? "Invalid request.");

    private ObjectResult Conflict(string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = detail,
        };
        problem.Extensions["code"] = code;

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
    }
}