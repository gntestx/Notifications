using Notifications.Api.Services;
using Notifications.Shared;
using Xunit;

namespace Notifications.Api.Tests;

public sealed class RequestValidationTests
{
    [Fact]
    public void ValidSubscriptionHasNoErrors()
    {
        var request = new PushSubscriptionRequest(
            "https://push.example.test/subscription/123",
            new PushSubscriptionKeys("public-key", "auth-key"));

        var errors = RequestValidation.Validate(request);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("http://push.example.test/subscription")]
    [InlineData("not a url")]
    [InlineData("")]
    public void InvalidSubscriptionEndpointIsRejected(string endpoint)
    {
        var request = new PushSubscriptionRequest(
            endpoint,
            new PushSubscriptionKeys("public-key", "auth-key"));

        var errors = RequestValidation.Validate(request);

        Assert.Contains(nameof(request.Endpoint), errors.Keys);
    }

    [Fact]
    public void UnsafeNotificationUrlIsRejected()
    {
        var request = new NotificationRequest("Rubrik", "Text", "javascript:alert(1)");

        var errors = RequestValidation.Validate(request);

        Assert.Contains(nameof(request.Url), errors.Keys);
    }

    [Fact]
    public void RelativeNotificationUrlIsAccepted()
    {
        var request = new NotificationRequest("Rubrik", "Text", "/meddelanden/42");

        var errors = RequestValidation.Validate(request);

        Assert.Empty(errors);
    }

    [Fact]
    public void OversizedNotificationIsRejected()
    {
        var request = new NotificationRequest(new string('R', 81), new string('M', 241));

        var errors = RequestValidation.Validate(request);

        Assert.Contains(nameof(request.Title), errors.Keys);
        Assert.Contains(nameof(request.Body), errors.Keys);
    }
}
