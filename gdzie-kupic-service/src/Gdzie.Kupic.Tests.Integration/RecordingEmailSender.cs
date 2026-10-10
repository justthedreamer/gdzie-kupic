using System.Collections.Concurrent;
using Gdzie.Kupic.Notifications;

namespace Gdzie.Kupic.Tests.Integration;

/// <summary>Records the e-mails instead of sending them; a failure can be scripted.</summary>
public sealed class RecordingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    public Exception? Failure { get; set; }

    public Func<EmailMessage, bool>? FailWhen { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (Failure is not null || FailWhen?.Invoke(message) == true) throw Failure ?? new InvalidOperationException("send failed");

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sent.Clear();
        Failure = null;
        FailWhen = null;
    }
}
