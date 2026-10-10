namespace Gdzie.Kupic.Realtime;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

/// <summary>
/// The only hub of the service. It has no client-callable methods: on connect the connection joins the group of its
/// own user (taken from the validated token) and every event is pushed by the server.
/// </summary>
[Authorize]
public sealed class AppHub : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        // The JWT handler keeps claim types as issued, so the subject stays "sub".
        if (!Guid.TryParse(Context.User?.FindFirstValue("sub"), out var userId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }
}