namespace Notifications.Shared;

public sealed record PushSubscriptionKeys(string P256dh, string Auth);

public sealed record PushSubscriptionRequest(
    string Endpoint,
    PushSubscriptionKeys Keys);

public sealed record NotificationRequest(
    string Title,
    string Body,
    string? Url = null);

public sealed record NotificationSendResult(
    int Subscriptions,
    int Delivered,
    int Failed,
    int Removed);

public sealed record PublicConfiguration(string VapidPublicKey);

public sealed record SubscriptionState(bool IsSupported, string Permission, bool IsSubscribed);
