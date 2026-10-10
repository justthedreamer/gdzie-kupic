namespace Gdzie.Kupic.Notifications;

public enum PushError
{
    None,
    Validation,
}

public sealed record PushResult(PushError Error, string? Message = null)
{
    public bool IsSuccess => Error == PushError.None;
}

public interface IPushSubscriptionService
{
    /// <summary>The VAPID public key, or null when Web Push is not configured.</summary>
    string? GetVapidPublicKey();

    /// <summary>Registers (or updates, or moves to the user) the subscription of a device; idempotent.</summary>
    Task<PushResult> RegisterAsync(Guid userId, string? endpoint, string? p256dhKey, string? authKey, CancellationToken ct = default);

    /// <summary>Removes the user's own subscription for the endpoint; idempotent.</summary>
    Task<PushResult> UnregisterAsync(Guid userId, string? endpoint, CancellationToken ct = default);
}
