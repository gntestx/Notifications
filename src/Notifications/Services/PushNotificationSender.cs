using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Notifications.Options;
using Notifications.Shared;
using WebPush;

namespace Notifications.Services;

public sealed class PushNotificationSender(
    IPushSubscriptionStore store,
    IOptions<PushOptions> options,
    ILogger<PushNotificationSender> logger)
{
    public async Task<SendNotificationResult> SendAsync(
        NotificationRequest notification,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.VapidPublicKey) ||
            string.IsNullOrWhiteSpace(settings.VapidPrivateKey) ||
            string.IsNullOrWhiteSpace(settings.Subject))
        {
            throw new InvalidOperationException("VAPID är inte fullständigt konfigurerat på servern.");
        }

        var payload = JsonSerializer.Serialize(new
        {
            notification.Title,
            notification.Body,
            notification.Url
        });
        var vapidDetails = new VapidDetails(
            settings.Subject,
            settings.VapidPublicKey,
            settings.VapidPrivateKey);

        var sent = 0;
        var removed = 0;
        var failed = 0;
        using var client = new WebPushClient();

        await foreach (var item in store.GetAllAsync(cancellationToken))
        {
            try
            {
                var subscription = new PushSubscription(item.Endpoint, item.Keys.P256dh, item.Keys.Auth);
                await client.SendNotificationAsync(subscription, payload, vapidDetails, cancellationToken);
                sent++;
            }
            catch (WebPushException exception) when (
                exception.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await store.DeleteAsync(item.Endpoint, cancellationToken);
                removed++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;
                logger.LogWarning(exception, "Web Push kunde inte skickas till {Endpoint}.", item.Endpoint);
            }
        }

        return new SendNotificationResult(sent, removed, failed);
    }
}

