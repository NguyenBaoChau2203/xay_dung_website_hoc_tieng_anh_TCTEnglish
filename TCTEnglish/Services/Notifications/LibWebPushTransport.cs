using System.Net;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.Extensions.Options;

namespace TCTEnglish.Services.Notifications;

public enum PushSendResult
{
    Sent,
    NotConfigured,
    Gone,
    RetryLater,
    Unknown
}

public interface IWebPushTransport
{
    Task<PushSendResult> SendAsync(string endpoint, string p256dh, string auth, string payloadJson, CancellationToken cancellationToken = default);
}

public sealed class LibWebPushTransport : IWebPushTransport
{
    private readonly IOptions<OutboundDeliveryOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;

    public LibWebPushTransport(IOptions<OutboundDeliveryOptions> options, IHttpClientFactory httpClientFactory)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PushSendResult> SendAsync(string endpoint, string p256dh, string auth, string payloadJson, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.PushConfigured)
        {
            return PushSendResult.NotConfigured;
        }

        if (!WebPushEndpointGuard.TryNormalize(endpoint, out var uri, out _))
        {
            return PushSendResult.Gone;
        }

        var client = new PushServiceClient(_httpClientFactory.CreateClient("webpush"))
        {
            DefaultAuthentication = new VapidAuthentication(options.VapidPublicKey!, options.VapidPrivateKey!)
            {
                Subject = options.VapidSubject
            }
        };
        var subscription = new PushSubscription
        {
            Endpoint = uri.AbsoluteUri
        };
        subscription.SetKey(PushEncryptionKeyName.P256DH, p256dh);
        subscription.SetKey(PushEncryptionKeyName.Auth, auth);

        try
        {
            await client.RequestPushMessageDeliveryAsync(subscription, new PushMessage(payloadJson), cancellationToken);
            return PushSendResult.Sent;
        }
        catch (PushServiceClientException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.Gone;
        }
        catch (PushServiceClientException ex) when ((int)ex.StatusCode == 429)
        {
            return PushSendResult.RetryLater;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return PushSendResult.Unknown;
        }
        catch (HttpRequestException)
        {
            return PushSendResult.Unknown;
        }
    }
}
