namespace Notifications.Api.Options;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string Subject { get; init; } = string.Empty;
    public string PublicKey { get; init; } = string.Empty;
    public string PrivateKey { get; init; } = string.Empty;
}
