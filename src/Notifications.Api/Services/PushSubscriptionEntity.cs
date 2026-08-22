using Azure;
using Azure.Data.Tables;

namespace Notifications.Api.Services;

public sealed class PushSubscriptionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "subscriptions";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
