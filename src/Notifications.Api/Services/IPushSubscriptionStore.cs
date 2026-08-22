using Notifications.Shared;

namespace Notifications.Api.Services;

public interface IPushSubscriptionStore
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken);
    Task UpsertAsync(PushSubscriptionRequest subscription, CancellationToken cancellationToken);
    Task DeleteAsync(string endpoint, CancellationToken cancellationToken);
    Task<IReadOnlyList<PushSubscriptionRequest>> GetAllAsync(CancellationToken cancellationToken);
}
