namespace Gdzie.Kupic.Notifications;

public sealed record EmailMessage(string To, string Subject, string Body);

/// <summary>Sends one e-mail. A failure throws, so that the background job is retried.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
