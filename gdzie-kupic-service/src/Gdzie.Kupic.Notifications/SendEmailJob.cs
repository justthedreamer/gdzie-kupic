namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Domain.Model;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Options;

/// <summary>Sends one e-mail to a buyer. Opt-in and ban are checked again here; a failure throws so Hangfire retries.</summary>
internal sealed class SendEmailJob(
    INotificationStorage storage,
    IEmailSender sender,
    IOptions<AppLinksSettings> app)
{
    public async Task RunAsync(Guid userId, NotificationKind kind, Guid? postId, Guid? threadId)
    {
        var recipient = await storage.FindEmailRecipientAsync(userId);
        if (!IsEligible(recipient)) return;

        await sender.SendAsync(EmailComposer.ForBuyer(recipient!, kind, postId, threadId, app.Value));
    }

    public static bool IsEligible(EmailRecipient? recipient) =>
        recipient is { EmailEnabled: true, Banned: false, Role: Role.Buyer };
}
