using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor.Services;
using Notifications.Components;
using Notifications.Client.Services;
using Notifications.Push;
using Notifications.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services
    .AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddMudServices();
builder.Services.AddHttpClient<PushNotificationClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost");
});
builder.Services.AddOptions<PushOptions>()
    .Bind(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddSingleton<IPushSubscriptionStore>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration["Storage:ConnectionString"];
    var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

    return string.IsNullOrWhiteSpace(connectionString)
        ? new InMemoryPushSubscriptionStore()
        : new AzureTablePushSubscriptionStore(connectionString, loggerFactory.CreateLogger<AzureTablePushSubscriptionStore>());
});
builder.Services.AddSingleton<PushNotificationSender>();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("send-notifications", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAntiforgery();
app.MapStaticAssets();

var pushApi = app.MapGroup("/api/push");

pushApi.MapGet("/public-key", Results<Ok<object>, ProblemHttpResult> (IConfiguration configuration) =>
{
    var publicKey = configuration["Push:VapidPublicKey"];
    return string.IsNullOrWhiteSpace(publicKey)
        ? TypedResults.Problem("VAPID är inte konfigurerat på servern.", statusCode: StatusCodes.Status503ServiceUnavailable)
        : TypedResults.Ok<object>(new { PublicKey = publicKey });
});

pushApi.MapPost("/subscriptions", async Task<Results<NoContent, ValidationProblem>> (
    PushSubscriptionDto subscription,
    IPushSubscriptionStore store,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateSubscription(subscription);
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }

    await store.UpsertAsync(subscription, cancellationToken);
    return TypedResults.NoContent();
});

pushApi.MapDelete("/subscriptions", async Task<Results<NoContent, ValidationProblem>> (
    [Microsoft.AspNetCore.Mvc.FromBody] PushSubscriptionDto subscription,
    IPushSubscriptionStore store,
    CancellationToken cancellationToken) =>
{
    var errors = ValidateSubscription(subscription);
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }

    await store.DeleteAsync(subscription.Endpoint, cancellationToken);
    return TypedResults.NoContent();
});

app.MapPost("/api/notifications/send", async Task<Results<Ok<SendNotificationResult>, ValidationProblem, UnauthorizedHttpResult, ProblemHttpResult>> (
    HttpRequest request,
    NotificationRequest notification,
    IConfiguration configuration,
    PushNotificationSender sender,
    CancellationToken cancellationToken) =>
{
    if (!HasValidAdminKey(request, configuration["Push:AdminKey"]))
    {
        return TypedResults.Unauthorized();
    }

    var errors = ValidateNotification(notification);
    if (errors.Count > 0)
    {
        return TypedResults.ValidationProblem(errors);
    }

    try
    {
        return TypedResults.Ok(await sender.SendAsync(notification, cancellationToken));
    }
    catch (InvalidOperationException exception)
    {
        return TypedResults.Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.RequireRateLimiting("send-notifications");

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Notifications.Client._Imports).Assembly);

app.Run();

static Dictionary<string, string[]> ValidateSubscription(PushSubscriptionDto subscription)
{
    var errors = new Dictionary<string, string[]>();

    if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
    {
        errors[nameof(subscription.Endpoint)] = ["Endpoint måste vara en giltig HTTPS-adress."];
    }

    if (string.IsNullOrWhiteSpace(subscription.Keys?.P256dh) || subscription.Keys.P256dh.Length > 256)
    {
        errors["Keys.P256dh"] = ["p256dh saknas eller är för lång."];
    }

    if (string.IsNullOrWhiteSpace(subscription.Keys?.Auth) || subscription.Keys.Auth.Length > 128)
    {
        errors["Keys.Auth"] = ["auth saknas eller är för lång."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateNotification(NotificationRequest notification)
{
    var context = new ValidationContext(notification);
    var validationResults = new List<ValidationResult>();
    Validator.TryValidateObject(notification, context, validationResults, validateAllProperties: true);

    var errors = validationResults
        .SelectMany(result => result.MemberNames.DefaultIfEmpty(string.Empty)
            .Select(member => (member, result.ErrorMessage ?? "Ogiltigt värde.")))
        .GroupBy(item => item.member)
        .ToDictionary(group => group.Key, group => group.Select(item => item.Item2).ToArray());

    if (string.IsNullOrWhiteSpace(notification.Url))
    {
        notification.Url = "/";
    }
    else if (!notification.Url.StartsWith("/", StringComparison.Ordinal) || notification.Url.StartsWith("//", StringComparison.Ordinal))
    {
        errors[nameof(notification.Url)] = ["Länken måste vara en relativ sökväg som börjar med ett enkelt /. "];
    }

    return errors;
}

static bool HasValidAdminKey(HttpRequest request, string? configuredKey)
{
    if (string.IsNullOrWhiteSpace(configuredKey) ||
        !request.Headers.TryGetValue("X-Notifications-Key", out var suppliedKey))
    {
        return false;
    }

    var expected = Encoding.UTF8.GetBytes(configuredKey);
    var supplied = Encoding.UTF8.GetBytes(suppliedKey.ToString());
    return expected.Length == supplied.Length && CryptographicOperations.FixedTimeEquals(expected, supplied);
}
