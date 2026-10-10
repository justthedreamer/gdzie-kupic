namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Domain.Model.Marketplace;
using Gdzie.Kupic.Marketplace;
using Gdzie.Kupic.Service.API.Contract.Merchant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = nameof(Role.Merchant))]
[Route("api/merchant/feed")]
public class MerchantFeedController(IMerchantResponseService responseService) : ControllerBase
{
    [HttpPut("{postId:guid}/response")]
    [ProducesResponseType<Feed.RespondResponse>(StatusCodes.Status200OK)]
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