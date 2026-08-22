namespace Notifications.Shared;

public sealed record PushSubscriptionDto(
    string Endpoint,
    PushSubscriptionKeys Keys);

public sealed record PushSubscriptionKeys(
    string P256dh,
    string Auth);

