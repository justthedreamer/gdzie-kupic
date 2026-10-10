namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Storage;

public interface INotificationSettingsService
{
    /// <summary>The e-mail setting of the user, or null when the account does not exist.</summary>
    Task<bool?> GetEmailEnabledAsync(Guid userId);

    Task<bool> SetEmailEnabledAsync(Guid userId, bool enabled);
}

internal sealed class NotificationSettingsService(INotificationStorage storage) : INotificationSettingsService
{
    public Task<bool?> GetEmailEnabledAsync(Guid userId) => storage.GetEmailEnabledAsync(userId);

    public Task<bool> SetEmailEnabledAsync(Guid userId, bool enabled) => storage.SetEmailEnabledAsync(userId, enabled);
}
