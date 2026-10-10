namespace Gdzie.Kupic.Chat;

/// <summary>Real-time push of chat events (implemented by the Realtime module). Recipients are user ids.</summary>
public interface IChatChannel
{
    Task MessageReceivedAsync(Guid userId, Guid threadId, Guid messageId, CancellationToken ct = default);

    Task ThreadUpdatedAsync(Guid userId, Guid threadId, CancellationToken ct = default);
}

internal sealed class NullChatChannel : IChatChannel
{
    public Task MessageReceivedAsync(Guid userId, Guid threadId, Guid messageId, CancellationToken ct = default) => Task.CompletedTask;
    public Task ThreadUpdatedAsync(Guid userId, Guid threadId, CancellationToken ct = default) => Task.CompletedTask;
}
