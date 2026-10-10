namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Posts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Buyer))]
[Route("api/posts")]
public class PostsController(IPostService postService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<Posts.PostWithCountDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] Posts.CreateRequest request, CancellationToken ct)
    {
        var input = new CreatePostInput(
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.CategoryId,
            request.TagId,
            request.Title,
            request.Description,
            request.UrgentDeadline);

        var result = await postService.CreateAsync(User.GetUserId(), input, ct);

        return result.IsSuccess
            ? Created($"/api/posts/{result.Value!.Post.Id}", PostMapping.ToDto(result.Value))
            : ToProblem(result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<Posts.PostWithCountDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List([FromQuery] string? scope, CancellationToken ct)
    {
        var parsed = scope?.ToLowerInvariant() switch
        {
            "active" => PostScope.Active,
            "ended" => PostScope.Ended,
            _ => (PostScope?)null,
        };

        if (parsed is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: "Scope must be 'active' or 'ended'.");

        var result = await postService.ListAsync(User.GetUserId(), parsed.Value, ct);

        return Ok(result.Value!.Select(PostMapping.ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Posts.PostWithCountDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await postService.GetAsync(User.GetUserId(), id, ct);

        return result.IsSuccess ? Ok(PostMapping.ToDto(result.Value!)) : ToProblem(result);
    }

    [HttpPost("{id:guid}/fulfil")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Fulfil(Guid id, CancellationToken ct)
    {
        var result = await postService.FulfilAsync(User.GetUserId(), id, ct);

        return result.IsSuccess ? NoContent() : ToProblem(result);
    }

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, CancellationToken ct)
    {
        var result = await postService.CloseAsync(User.GetUserId(), id, ct);

        return result.IsSuccess ? NoContent() : ToProblem(result);
    }

    private IActionResult ToProblem<T>(PostResult<T> result)
    {
        var (status, title) = result.Error switch
        {
            PostError.NotFound => (StatusCodes.Status404NotFound, "Post not found"),
            PostError.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Validation error"),
        };

        return Problem(statusCode: status, title: title, detail: result.Message);
    }
}
