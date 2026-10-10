namespace Gdzie.Kupic.Service.API.Controllers;

using Gdzie.Kupic.Chat;
using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Service.API.Contract.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Authorize(Roles = $"{nameof(Role.Buyer)},{nameof(Role.Merchant)}")]
[Route("api/chat")]
public class ChatController(IChatService chatService) : ControllerBase
{
    private const int PreviewLength = 100;

    private Role CallerRole => User.IsInRole(nameof(Role.Merchant)) ? Role.Merchant : Role.Buyer;

    [HttpGet("threads")]
    [ProducesResponseType<Chat.ThreadPage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListThreads([FromQuery] string? cursor, [FromQuery] int? limit, CancellationToken ct)
    {
        var result = await chatService.ListThreadsAsync(User.GetUserId(), CallerRole, cursor, limit, ct);

        return result.IsSuccess
            ? Ok(new Chat.ThreadPage(result.Value!.Items.Select(ToDto).ToList(), result.Value.NextCursor))
            : ToProblem(result);
    }

    [HttpGet("threads/{id:guid}")]
    [ProducesResponseType<Chat.ThreadSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThread(Guid id, CancellationToken ct)
    {
        var result = await chatService.GetThreadAsync(User.GetUserId(), CallerRole, id, ct);

        return result.IsSuccess ? Ok(ToDto(result.Value!)) : ToProblem(result);
    }

    [HttpGet("threads/{id:guid}/messages")]
    [ProducesResponseType<Chat.MessagePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages(
        Guid id, [FromQuery] Guid? before, [FromQuery] Guid? after, [FromQuery] int? limit, CancellationToken ct)
    {
        var result = await chatService.GetMessagesAsync(User.GetUserId(), CallerRole, id, before, after, limit, ct);

        return result.IsSuccess
            ? Ok(new Chat.MessagePage(result.Value!.Items.Select(m => ToDto(m)).ToList(), result.Value.HasMore))
            : ToProblem(result);
    }

    [HttpPost("threads/{id:guid}/messages")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    [ProducesResponseType<Chat.Message>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType)]
    public async Task<IActionResult> Send(Guid id, [FromForm] string? body, IFormFile? image, CancellationToken ct)
    {
        await using var content = image is { Length: > 0 } ? image.OpenReadStream() : null;
        var attachment = content is null ? null : new ChatAttachmentInput(content, image!.ContentType, image.Length);

        var result = await chatService.SendAsync(User.GetUserId(), CallerRole, id, body, attachment, ct);

        return result.IsSuccess
            ? Created($"/api/chat/threads/{id}/messages", ToDto(result.Value!))
            : ToProblem(result);
    }

    [HttpPost("threads/{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var result = await chatService.MarkReadAsync(User.GetUserId(), CallerRole, id, ct);

        return result.IsSuccess ? NoContent() : ToProblem(result);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType<Chat.UnreadCount>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct)
    {
        var result = await chatService.GetUnreadCountAsync(User.GetUserId(), CallerRole, ct);

        return result.IsSuccess ? Ok(new Chat.UnreadCount(result.Value)) : ToProblem(result);
    }

    private static Chat.ThreadSummary ToDto(ChatThreadView v)
    {
        var t = v.Thread;

        return new Chat.ThreadSummary(
            t.Id,
            new Chat.ThreadPost(t.PostId, t.PostTitle, t.PostStatus.ToString()),
            new Chat.Counterpart(v.CounterpartId, v.CounterpartName ?? string.Empty),
            t.LastMessageAt is { } at
                ? new Chat.LastMessage(Preview(t.LastBody), at, v.LastMessageIsMine)
                : null,
            t.UnreadCount,
            t.IsLocked,
            t.CreatedAt);
    }

    private static Chat.Message ToDto(ChatMessageView v) =>
        new(v.Message.Id, v.Message.ThreadId, v.Message.SenderId, v.IsMine, v.Message.Body, v.AttachmentUrl, v.Message.CreatedAt);

    private static string Preview(string? body) =>
        body is null ? string.Empty : body.Length <= PreviewLength ? body : body[..PreviewLength];

    private static ObjectResult CodedProblem(int status, string title, string? detail, string code)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        problem.Extensions["code"] = code;

        return new ObjectResult(problem) { StatusCode = status };
    }

    private IActionResult ToProblem<T>(ChatResult<T> result)
    {
        switch (result.Error)
        {
            case ChatError.NotFound:
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Conversation not found");
            case ChatError.ThreadLocked:
                return CodedProblem(StatusCodes.Status403Forbidden, "Forbidden", result.Message, "thread_locked");
            case ChatError.AttachmentTooLarge:
                return CodedProblem(StatusCodes.Status413PayloadTooLarge, "Attachment too large", result.Message, "attachment_too_large");
            case ChatError.UnsupportedAttachmentType:
                return CodedProblem(StatusCodes.Status415UnsupportedMediaType, "Unsupported attachment type", result.Message, "unsupported_attachment_type");
            default:
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Validation error", detail: result.Message);
        }
    }
}