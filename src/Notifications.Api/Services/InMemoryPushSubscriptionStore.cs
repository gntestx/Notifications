using System.Collections.Concurrent;
using Notifications.Shared;

namespace Notifications.Api.Services;

public sealed class InMemoryPushSubscriptionStore : IPushSubscriptionStore
{
    private readonly ConcurrentDictionary<string, PushSubscriptionRequest> _subscriptions = new();

    public Task EnsureCreatedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UpsertAsync(PushSubscriptionRequest subscription, CancellationToken cancellationToken)
    {
        _subscriptions[SubscriptionKey.FromEndpoint(subscription.Endpoint)] = subscription;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        _subscriptions.TryRemove(SubscriptionKey.FromEndpoint(endpoint), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PushSubscriptionRequest>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PushSubscriptionRequest>>(_subscriptions.Values.ToArray());
}
