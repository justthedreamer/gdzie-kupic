namespace Gdzie.Kupic.Notifications;

public sealed record WebPushTarget(string Endpoint, string P256dhKey, string AuthKey);

public enum WebPushOutcome
{
    Delivered,

    /// <summary>The push service answered 404 / 410: the subscription no longer exists.</summary>
    SubscriptionGone,

    /// <summary>A permanent refusal (other 4xx): not retried, the subscription is kept.</summary>
    Rejected,
}

/// <summary>Sends one Web Push message. Transient failures (5xx, 429, network) are thrown so that the job is retried.</summary>
public interface IWebPushSender
{
    Task<WebPushOutcome> SendAsync(WebPushTarget target, string payloadJson, CancellationToken ct = default);
}
