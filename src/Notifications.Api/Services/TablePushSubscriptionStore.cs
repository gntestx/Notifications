using Azure.Data.Tables;
using Notifications.Shared;

namespace Notifications.Api.Services;

public sealed class TablePushSubscriptionStore(IConfiguration configuration) : IPushSubscriptionStore
{
    private readonly TableClient _table = new(
        configuration["Storage:TableConnectionString"]
            ?? throw new InvalidOperationException("Storage:TableConnectionString is missing."),
        configuration["Storage:TableName"] ?? "PushSubscriptions");

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken) =>
        await _table.CreateIfNotExistsAsync(cancellationToken);

    public async Task UpsertAsync(PushSubscriptionRequest subscription, CancellationToken cancellationToken)
    {
        var entity = new PushSubscriptionEntity
        {
            RowKey = SubscriptionKey.FromEndpoint(subscription.Endpoint),
            Endpoint = subscription.Endpoint,
            P256dh = subscription.Keys.P256dh,
            Auth = subscription.Keys.Auth,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            await _table.DeleteEntityAsync("subscriptions", SubscriptionKey.FromEndpoint(endpoint), cancellationToken: cancellationToken);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            // Deleting an already removed subscription is intentionally idempotent.
        }
    }

    public async Task<IReadOnlyList<PushSubscriptionRequest>> GetAllAsync(CancellationToken cancellationToken)
    {
        var subscriptions = new List<PushSubscriptionRequest>();
        await foreach (var entity in _table.QueryAsync<PushSubscriptionEntity>(
                           item => item.PartitionKey == "subscriptions",
                           cancellationToken: cancellationToken))
        {
            subscriptions.Add(new PushSubscriptionRequest(
                entity.Endpoint,
                new PushSubscriptionKeys(entity.P256dh, entity.Auth)));
        }

        return subscriptions;
    }
}
