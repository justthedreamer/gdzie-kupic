namespace Gdzie.Kupic.Notifications;

using Gdzie.Kupic.Domain.Model.Notifications;
using Gdzie.Kupic.Storage;
using Microsoft.Extensions.Options;

internal sealed class PushSubscriptionService(
    INotificationStorage storage,
    IOptions<VapidSettings> vapid,
    TimeProvider clock) : IPushSubscriptionService
{
    public string? GetVapidPublicKey() => vapid.Value.IsConfigured ? vapid.Value.PublicKey : null;

    public async Task<PushResult> RegisterAsync(
        Guid userId, string? endpoint, string? p256dhKey, string? authKey, CancellationToken ct = default)
    {
        var error = ValidateEndpoint(endpoint) ?? ValidateKey(p256dhKey, "p256dh") ?? ValidateKey(authKey, "auth");
        if (error is not null) return new PushResult(PushError.Validation, error);

        await storage.UpsertPushSubscriptionAsync(userId, endpoint!.Trim(), p256dhKey!.Trim(), authKey!.Trim(), clock.GetUtcNow(), ct);

        return new PushResult(PushError.None);
    }

    public async Task<PushResult> UnregisterAsync(Guid userId, string? endpoint, CancellationToken ct = default)
    {
        var error = ValidateEndpoint(endpoint);
        if (error is not null) return new PushResult(PushError.Validation, error);

        await storage.RemovePushSubscriptionAsync(userId, endpoint!.Trim(), ct);

        return new PushResult(PushError.None);
    }

    private static string? ValidateEndpoint(string? endpoint)
    {
        var value = endpoint?.Trim();
        if (string.IsNullOrEmpty(value)) return "Endpoint must not be empty.";
        if (value.Length > PushSubscription.MaxEndpointLength)
            return $"Endpoint must not exceed {PushSubscription.MaxEndpointLength} characters.";

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? null
            : "Endpoint must be an absolute HTTPS URL.";
    }

    private static string? ValidateKey(string? key, string name)
    {
        var value = key?.Trim();
        if (string.IsNullOrEmpty(value)) return $"Key {name} must not be empty.";

        return value.Length > PushSubscription.MaxKeyLength ? $"Key {name} must not exceed {PushSubscription.MaxKeyLength} characters." : null;
    }
}
