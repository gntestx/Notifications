using System.Net;
using System.Net.Http.Json;
using Microsoft.JSInterop;
using Notifications.Shared;

namespace Notifications.Client.Services;

public sealed class PushNotificationClient(HttpClient httpClient, IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<bool> IsSupportedAsync() =>
        await (await ModuleAsync()).InvokeAsync<bool>("isSupported");

    public async Task<bool> IsSubscribedAsync() =>
        await (await ModuleAsync()).InvokeAsync<bool>("isSubscribed");

    public async Task SubscribeAsync()
    {
        var keyResponse = await httpClient.GetFromJsonAsync<PublicKeyResponse>("api/push/public-key")
            ?? throw new InvalidOperationException("Servern returnerade ingen VAPID-nyckel.");

        var subscription = await (await ModuleAsync())
            .InvokeAsync<PushSubscriptionDto>("subscribe", keyResponse.PublicKey);

        using var response = await httpClient.PostAsJsonAsync("api/push/subscriptions", subscription);
        await EnsureSuccessAsync(response, "Det gick inte att registrera prenumerationen.");
    }

    public async Task UnsubscribeAsync()
    {
        var subscription = await (await ModuleAsync())
            .InvokeAsync<PushSubscriptionDto?>("getSubscription");

        if (subscription is not null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, "api/push/subscriptions")
            {
                Content = JsonContent.Create(subscription)
            };
            using var response = await httpClient.SendAsync(request);
            await EnsureSuccessAsync(response, "Det gick inte att ta bort prenumerationen.");
        }

        await (await ModuleAsync()).InvokeVoidAsync("unsubscribe");
    }

    public async Task<SendNotificationResult> SendAsync(NotificationRequest notification, string adminKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/notifications/send")
        {
            Content = JsonContent.Create(notification)
        };
        request.Headers.Add("X-Notifications-Key", adminKey);

        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response, "Notisen kunde inte skickas.");
        return await response.Content.ReadFromJsonAsync<SendNotificationResult>()
            ?? new SendNotificationResult(0, 0, 0);
    }

    private async ValueTask<IJSObjectReference> ModuleAsync() =>
        _module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./js/push-notifications.js");

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            message = "Sändningsnyckeln är felaktig.";
        }

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? fallbackMessage : message.Trim('"'));
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            await _module.DisposeAsync();
        }
    }

    private sealed record PublicKeyResponse(string PublicKey);
}

