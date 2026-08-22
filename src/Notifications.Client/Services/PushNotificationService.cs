using System.Net.Http.Json;
using Microsoft.JSInterop;
using Notifications.Shared;

namespace Notifications.Client.Services;

public sealed class PushNotificationService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public Task<SubscriptionState> GetStateAsync() =>
        jsRuntime.InvokeAsync<SubscriptionState>("pushNotifications.getState").AsTask();

    public async Task SubscribeAsync()
    {
        var configuration = await httpClient.GetFromJsonAsync<PublicConfiguration>("api/config")
            ?? throw new InvalidOperationException("API:t returnerade ingen VAPID-konfiguration.");

        var subscription = await jsRuntime.InvokeAsync<PushSubscriptionRequest>(
            "pushNotifications.subscribe",
            configuration.VapidPublicKey);

        using var response = await httpClient.PostAsJsonAsync("api/subscriptions", subscription);
        await EnsureSuccessAsync(response);
    }

    public async Task UnsubscribeAsync()
    {
        var subscription = await jsRuntime.InvokeAsync<PushSubscriptionRequest?>(
            "pushNotifications.getSubscription");

        if (subscription is null)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Delete, "api/subscriptions")
        {
            Content = JsonContent.Create(subscription)
        };
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        await jsRuntime.InvokeVoidAsync("pushNotifications.unsubscribe");
    }

    public async Task<NotificationSendResult> SendAsync(NotificationRequest notification, string adminKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/notifications")
        {
            Content = JsonContent.Create(notification)
        };
        request.Headers.Add("X-Admin-Key", adminKey);

        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<NotificationSendResult>()
            ?? throw new InvalidOperationException("API:t returnerade inget utskicksresultat.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(
            response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                ? "Administratörsnyckeln är felaktig."
                : $"API-anropet misslyckades ({(int)response.StatusCode}): {details}");
    }
}
