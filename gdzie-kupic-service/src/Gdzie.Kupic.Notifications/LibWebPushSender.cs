namespace Gdzie.Kupic.Notifications;

using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

internal sealed class LibWebPushSender(IOptions<VapidSettings> settings, ILogger<LibWebPushSender> logger) : IWebPushSender
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private PushServiceClient? _client;

    public async Task<WebPushOutcome> SendAsync(WebPushTarget target, string payloadJson, CancellationToken ct = default)
    {
        var subscription = new Lib.Net.Http.WebPush.PushSubscription
        {
            Endpoint = target.Endpoint,
            Keys = new Dictionary<string, string>
            {
                ["p256dh"] = target.P256dhKey,
                ["auth"] = target.AuthKey,
            },
        };

        try
        {
            await Client().RequestPushMessageDeliveryAsync(subscription, new PushMessage(payloadJson), ct);

            return WebPushOutcome.Delivered;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return WebPushOutcome.SubscriptionGone;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError
                                                    && ex.StatusCode != HttpStatusCode.TooManyRequests)
        {
            logger.LogWarning("Push service refused a message with {StatusCode}; not retrying", (int)ex.StatusCode);

            return WebPushOutcome.Rejected;
        }
    }

    private PushServiceClient Client()
    {
        if (_client is not null) return _client;

        var vapid = settings.Value;
        _client = new PushServiceClient(Http)
        {
            DefaultAuthentication = new VapidAuthentication(vapid.PublicKey, vapid.PrivateKey) { Subject = vapid.Subject },
        };

        return _client;
    }
}
