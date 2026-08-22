using System.Security.Cryptography;
using System.Text;

namespace Notifications.Api.Services;

public static class AdminKeyAuthorization
{
    public static bool IsAuthorized(HttpRequest request, IConfiguration configuration)
    {
        var expected = configuration["AdminKey"];
        var supplied = request.Headers["X-Admin-Key"].ToString();
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied));
    }
}
