using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Notifications.Api.Options;
using Notifications.Api.Services;
using Notifications.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
builder.Services.AddMemoryCache();
builder.Services.AddMemoryVapidTokenCache();
builder.Services.AddPushServiceClient(options =>
{
    var section = builder.Configuration.GetSection(PushOptions.SectionName);
    options.Subject = section[nameof(PushOptions.Subject)] ?? string.Empty;
    options.PublicKey = section[nameof(PushOptions.PublicKey)] ?? string.Empty;
    options.PrivateKey = section[nameof(PushOptions.PrivateKey)] ?? string.Empty;
});
builder.Services.AddTransient<PushSender>();

var tableConnection = builder.Configuration["Storage:TableConnectionString"];
if (string.IsNullOrWhiteSpace(tableConnection))
{
    builder.Services.AddSingleton<IPushSubscriptionStore, InMemoryPushSubscriptionStore>();
}
else
{
    builder.Services.AddSingleton<IPushSubscriptionStore, TablePushSubscriptionStore>();
}

var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else if (builder.Environment.IsDevelopment())
    {
        policy.SetIsOriginAllowed(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback)
            .AllowAnyHeader()
            .AllowAnyMethod();
    }
}));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("subscriptions", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors();
app.UseRateLimiter();

var store = app.Services.GetRequiredService<IPushSubscriptionStore>();
await store.EnsureCreatedAsync(CancellationToken.None);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/config", (IOptions<PushOptions> options) =>
{
    var publicKey = options.Value.PublicKey;
    return string.IsNullOrWhiteSpace(publicKey)
        ? Results.Problem("VAPID public key is not configured.", statusCode: 503)
        : Results.Ok(new PublicConfiguration(publicKey));
});

app.MapPost("/api/subscriptions", async (
    PushSubscriptionRequest? request,
    IPushSubscriptionStore subscriptions,
    CancellationToken cancellationToken) =>
{
    var errors = RequestValidation.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    await subscriptions.UpsertAsync(request!, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("subscriptions");

app.MapDelete("/api/subscriptions", async (
    [FromBody] PushSubscriptionRequest? request,
    IPushSubscriptionStore subscriptions,
    CancellationToken cancellationToken) =>
{
    var errors = RequestValidation.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    await subscriptions.DeleteAsync(request!.Endpoint, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("subscriptions");

app.MapGet("/api/subscriptions/count", async (
    HttpRequest request,
    IConfiguration configuration,
    IPushSubscriptionStore subscriptions,
    CancellationToken cancellationToken) =>
{
    if (!AdminKeyAuthorization.IsAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var all = await subscriptions.GetAllAsync(cancellationToken);
    return Results.Ok(new { count = all.Count });
});

app.MapPost("/api/notifications", async (
    NotificationRequest? notification,
    HttpRequest request,
    IConfiguration configuration,
    PushSender sender,
    CancellationToken cancellationToken) =>
{
    if (!AdminKeyAuthorization.IsAuthorized(request, configuration))
    {
        return Results.Unauthorized();
    }

    var errors = RequestValidation.Validate(notification);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var result = await sender.SendAsync(notification!, cancellationToken);
    return Results.Ok(result);
});

app.Run();

public partial class Program;
