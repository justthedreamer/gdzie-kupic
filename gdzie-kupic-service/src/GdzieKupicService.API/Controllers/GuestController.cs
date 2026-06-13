namespace GdzieKupicService.API.Contracts;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/b/g")]
[Authorize]
public class GuestController : ControllerBase
{
    [HttpGet("posts")]
    public async Task<ActionResult<GuestContracts.GetPosts.Response>> GetPosts()
    {
        return this.NoContent();
    }
    
    [HttpPost("post/{postId:guid}/comment")]
    public async Task<IActionResult> AddPostComment(
        Guid postId,
        [FromBody] UserContracts.AddPostComment.Request request)
    {
        return this.NoContent();
    }

    [HttpPatch("post/{postId:guid}/comment/{commentId:guid}")]
    public async Task<IActionResult> EditPostComment(
        Guid postId,
        Guid commentId,
        [FromBody] UserContracts.EditPostComment.Request request)
    {
        return this.NoContent();
    }

    [HttpDelete("post/{postId:guid}/comment/{commentId:guid}")]
    public async Task<IActionResult> DeletePostComment(Guid postId, Guid commentId)
    {
        return this.NoContent();
    }
}