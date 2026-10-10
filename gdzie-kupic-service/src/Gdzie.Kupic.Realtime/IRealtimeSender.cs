namespace Gdzie.Kupic.Realtime;

using Microsoft.AspNetCore.SignalR;

/// <summary>Pushes a named event to every open connection of one user.</summary>
public interface IRealtimeSender
{
    Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default);
}

internal sealed class HubRealtimeSender(IHubContext<AppHub> hub) : IRealtimeSender
{
    public Task SendToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default) =>
        hub.Clients.Group(AppHub.UserGroup(userId)).SendAsync(eventName, payload, ct);
}