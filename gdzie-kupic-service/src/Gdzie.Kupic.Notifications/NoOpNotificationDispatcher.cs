namespace Gdzie.Kupic.Notifications;

/// <summary>Dispatcher that delivers nothing; used where no channel is wanted (e.g. unit tests).</summary>
internal sealed class NoOpNotificationDispatcher : INotificationDispatcher
{
    public Task DispatchAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
}
