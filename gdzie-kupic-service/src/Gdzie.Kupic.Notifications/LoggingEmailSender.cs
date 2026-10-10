namespace Gdzie.Kupic.Notifications;

using Microsoft.Extensions.Logging;

/// <summary>Default sender until a provider is chosen (FR-NOTIF-5): writes the message to the log.</summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogInformation("E-mail to {To}: {Subject}{NewLine}{Body}", message.To, message.Subject, Environment.NewLine, message.Body);

        return Task.CompletedTask;
    }
}
