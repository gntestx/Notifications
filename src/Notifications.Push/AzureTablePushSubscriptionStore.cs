using System.Net;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using Notifications.Shared;

namespace Notifications.Push;

public sealed class AzureTablePushSubscriptionStore : IPushSubscriptionStore
{
    private const string PartitionKey = "webpush";
    private readonly TableClient _tableClient;
    private readonly ILogger<AzureTablePushSubscriptionStore> _logger;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;

    public AzureTablePushSubscriptionStore(string connectionString, ILogger<AzureTablePushSubscriptionStore> logger)
    {
        _tableClient = new TableClient(connectionString, "PushSubscriptions");
        _logger = logger;
    }

    public async Task UpsertAsync(PushSubscriptionDto subscription, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        var entity = new PushSubscriptionEntity
        {
            PartitionKey = PartitionKey,
            RowKey = RowKeyFor(subscription.Endpoint),
            Endpoint = subscription.Endpoint,
            P256dh = subscription.Keys.P256dh,
            Auth = subscription.Keys.Auth
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        try
        {
            await _tableClient.DeleteEntityAsync(PartitionKey, RowKeyFor(endpoint), ETag.All, cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == (int)HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Prenumerationen fanns redan inte kvar.");
        }
    }

    public async IAsyncEnumerable<PushSubscriptionDto> GetAllAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken);
        await foreach (var entity in _tableClient.QueryAsync<PushSubscriptionEntity>(
                           item => item.PartitionKey == PartitionKey,
                           cancellationToken: cancellationToken))
        {
            yield return new PushSubscriptionDto(
                entity.Endpoint,
                new PushSubscriptionKeys(entity.P256dh, entity.Auth));
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (!_initialized)
            {
                await _tableClient.CreateIfNotExistsAsync(cancellationToken);
                _initialized = true;
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    private static string RowKeyFor(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));

    private sealed class PushSubscriptionEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string Endpoint { get; set; } = string.Empty;
        public string P256dh { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }
}
