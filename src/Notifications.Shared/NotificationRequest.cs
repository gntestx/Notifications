using System.ComponentModel.DataAnnotations;

namespace Notifications.Shared;

public sealed class NotificationRequest
{
    [Required, StringLength(80)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string Body { get; set; } = string.Empty;

    [StringLength(200)]
    public string Url { get; set; } = "/";
}

public sealed record SendNotificationResult(int Sent, int Removed, int Failed);

