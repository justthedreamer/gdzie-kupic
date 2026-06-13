namespace GdzieKupicService.API.Controllers;

using GdzieKupicService.API.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/b/m")]
[Authorize]
public class MerchantController : ControllerBase
{
    [HttpPost("post/{postId:guid}/comment")]
    public async Task<IActionResult> AddPostComment(
        Guid postId,
        [FromBody] MerchantContracts.AddPostComment.Request request)
    {
        return this.NoContent();
    }

    [HttpPatch("post/{postId:guid}/comment/{commentId:guid}")]
    public async Task<IActionResult> EditPostComment(
        Guid postId,
        Guid commentId,
        [FromBody] MerchantContracts.EditPostComment.Request request)
    {
        return this.NoContent();
    }

    [HttpDelete("post/{postId:guid}/comment/{commentId:guid}")]
    public async Task<IActionResult> DeletePostComment(Guid postId, Guid commentId)
    {
        return this.NoContent();
    }

    [HttpPost("post/{postId:guid}/comment")]
    public async Task<IActionResult> CommentPost(Guid postId)
    {
        return this.NoContent();
    }

    /// <summary>
    /// Allows merchant to get feed of posts that are relevant to them.
    /// </summary>
    /// <returns></returns>
    [HttpGet("feed")]
    public async Task<ActionResult<MerchantContracts.GetFeed.Response>> GetFeed()
    {
        return this.NoContent();
    }

    /// <summary>
    /// Gets responded posts.
    /// </summary>
    /// <returns></returns>
    [HttpGet("posts/active")]
    public async Task<ActionResult<MerchantContracts.GetActivePosts.Response>> GetActivePosts()
    {
        return this.NoContent();
    }
}