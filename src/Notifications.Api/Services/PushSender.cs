using System.Net;
using System.Text.Json;
using Lib.Net.Http.WebPush;
using Notifications.Shared;

namespace Notifications.Api.Services;

public sealed class PushSender(
    IPushSubscriptionStore store,
    PushServiceClient pushClient,
    ILogger<PushSender> logger)
{
    public async Task<NotificationSendResult> SendAsync(
        NotificationRequest notification,
        CancellationToken cancellationToken)
    {
        var subscriptions = await store.GetAllAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(notification);
        var delivered = 0;
        var failed = 0;
        var removed = 0;

        foreach (var item in subscriptions)
        {
            try
            {
                var subscription = new PushSubscription { Endpoint = item.Endpoint };
                subscription.SetKey(PushEncryptionKeyName.P256DH, item.Keys.P256dh);
                subscription.SetKey(PushEncryptionKeyName.Auth, item.Keys.Auth);
                var message = new PushMessage(payload)
                {
                    TimeToLive = 60 * 60 * 24,
                    Urgency = PushMessageUrgency.Normal
                };
                await pushClient.RequestPushMessageDeliveryAsync(subscription, message, cancellationToken);
                delivered++;
            }
            catch (PushServiceClientException exception) when (
                exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await store.DeleteAsync(item.Endpoint, cancellationToken);
                removed++;
            }
            catch (Exception exception) when (exception is PushServiceClientException or HttpRequestException)
            {
                logger.LogWarning(exception, "Push delivery failed for subscription {SubscriptionKey}",
                    SubscriptionKey.FromEndpoint(item.Endpoint));
                failed++;
            }
        }

        return new NotificationSendResult(subscriptions.Count, delivered, failed, removed);
    }
}
