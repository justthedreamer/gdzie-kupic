using System.Collections.Concurrent;
using Gdzie.Kupic.Notifications;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>Records the pushes instead of calling a push service; the next answer can be scripted.</summary>
public sealed class RecordingWebPushSender : IWebPushSender
{
    private readonly ConcurrentQueue<(WebPushTarget Target, string Payload)> _sent = new();

    public IReadOnlyList<(WebPushTarget Target, string Payload)> Sent => _sent.ToArray();

    public WebPushOutcome Outcome { get; set; } = WebPushOutcome.Delivered;

    public Exception? Failure { get; set; }

    public Task<WebPushOutcome> SendAsync(WebPushTarget target, string payloadJson, CancellationToken ct = default)
    {
        if (Failure is not null) throw Failure;

        _sent.Enqueue((target, payloadJson));
        return Task.FromResult(Outcome);
    }

    public void Reset()
    {
        _sent.Clear();
        Outcome = WebPushOutcome.Delivered;
        Failure = null;
    }
}
