using Notifications.Shared;

namespace Notifications.Push;

public interface IPushSubscriptionStore
{
    Task UpsertAsync(PushSubscriptionDto subscription, CancellationToken cancellationToken);
    Task DeleteAsync(string endpoint, CancellationToken cancellationToken);
    IAsyncEnumerable<PushSubscriptionDto> GetAllAsync(CancellationToken cancellationToken);
}
