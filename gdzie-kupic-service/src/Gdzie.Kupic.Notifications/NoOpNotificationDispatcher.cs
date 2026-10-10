namespace Gdzie.Kupic.Notifications;

/// <summary>Placeholder until real channel delivery exists (Phase 7).</summary>
internal sealed class NoOpNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(Guid postId, Guid merchantId, CancellationToken ct = default) => Task.CompletedTask;
}
