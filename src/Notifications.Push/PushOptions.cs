namespace Notifications.Push;

public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string VapidPublicKey { get; set; } = string.Empty;
    public string VapidPrivateKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string AdminKey { get; set; } = string.Empty;
}
