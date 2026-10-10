namespace Gdzie.Kupic.Service.API.Contract.Push;

public sealed class Push
{
    /// <param name="PublicKey">The VAPID application server key (URL-safe base64) for <c>pushManager.subscribe</c>.</param>
    public sealed record VapidKeyResponse(string PublicKey);

    public sealed record Keys(string P256dh, string Auth);

    /// <param name="Endpoint">The HTTPS endpoint of the push service for this device.</param>
    public sealed record SubscribeRequest(string Endpoint, Keys Keys);

    public sealed record UnsubscribeRequest(string Endpoint);
}
