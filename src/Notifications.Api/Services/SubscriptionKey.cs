using System.Security.Cryptography;
using System.Text;

namespace Notifications.Api.Services;

internal static class SubscriptionKey
{
    public static string FromEndpoint(string endpoint) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)));
}
