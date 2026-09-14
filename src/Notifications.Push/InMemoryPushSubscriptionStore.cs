using System.Collections.Concurrent;
using Notifications.Shared;

namespace Notifications.Push;

public sealed class InMemoryPushSubscriptionStore : IPushSubscriptionStore
{
    private readonly ConcurrentDictionary<string, PushSubscriptionDto> _subscriptions = new(StringComparer.Ordinal);

    public Task UpsertAsync(PushSubscriptionDto subscription, CancellationToken cancellationToken)
    {
        _subscriptions[subscription.Endpoint] = subscription;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        _subscriptions.TryRemove(endpoint, out _);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<PushSubscriptionDto> GetAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return subscription;
        }

        await Task.CompletedTask;
    }
}
