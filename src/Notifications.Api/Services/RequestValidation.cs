using Notifications.Shared;

namespace Notifications.Api.Services;

public static class RequestValidation
{
    public static Dictionary<string, string[]> Validate(PushSubscriptionRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["subscription"] = ["En prenumeration måste anges."];
            return errors;
        }

        if (!Uri.TryCreate(request.Endpoint, UriKind.Absolute, out var endpoint)
            || endpoint.Scheme != Uri.UriSchemeHttps
            || request.Endpoint.Length > 2048)
        {
            errors[nameof(request.Endpoint)] = ["Endpoint måste vara en giltig HTTPS-adress."];
        }

        if (string.IsNullOrWhiteSpace(request.Keys?.P256dh) || request.Keys.P256dh.Length > 512)
        {
            errors["Keys.P256dh"] = ["En giltig P256dh-nyckel krävs."];
        }

        if (string.IsNullOrWhiteSpace(request.Keys?.Auth) || request.Keys.Auth.Length > 256)
        {
            errors["Keys.Auth"] = ["En giltig auth-nyckel krävs."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> Validate(NotificationRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["notification"] = ["En notifikation måste anges."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 80)
        {
            errors[nameof(request.Title)] = ["Rubriken måste innehålla 1–80 tecken."];
        }

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 240)
        {
            errors[nameof(request.Body)] = ["Meddelandet måste innehålla 1–240 tecken."];
        }

        if (!string.IsNullOrWhiteSpace(request.Url)
            && (!Uri.TryCreate(request.Url, UriKind.RelativeOrAbsolute, out var url)
                || (url.IsAbsoluteUri && url.Scheme != Uri.UriSchemeHttps)))
        {
            errors[nameof(request.Url)] = ["Länken måste vara relativ eller använda HTTPS."];
        }

        return errors;
    }
}
