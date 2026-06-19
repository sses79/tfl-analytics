using TflAnalytics.Api.Hubs;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Realtime;
using TflAnalytics.Infrastructure;
using TflAnalytics.Infrastructure.Realtime;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(origin => origin.Value)
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Cast<string>()
    .ToArray();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProcessingInfrastructure(builder.Configuration);

// SignalR:ConnectionString supports the AAD connection-string format:
// Endpoint=https://<name>.service.signalr.net;AuthType=aad;Version=1.0;
var signalRConnectionString = builder.Configuration["SignalR:ConnectionString"];
var signalRBuilder = builder.Services.AddSignalR();
if (!string.IsNullOrWhiteSpace(signalRConnectionString))
{
    signalRBuilder.AddAzureSignalR(signalRConnectionString);
}

// Override the IRealtimeNotifier registered by AddProcessingInfrastructure with one backed
// by the in-process hub context, so API-side code uses the same connection as clients.
builder.Services.AddSingleton<IRealtimeNotifier, HubContextNotifier<DashboardHub>>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapPost(
        "/internal/realtime/arrivals",
        (ArrivalsUpdated message, IRealtimeNotifier notifier, CancellationToken cancellationToken) =>
            notifier.BroadcastArrivalsAsync(message, cancellationToken));
    app.MapPost(
        "/internal/realtime/line-status",
        (LineStatusChanged message, IRealtimeNotifier notifier, CancellationToken cancellationToken) =>
            notifier.BroadcastLineStatusAsync(message, cancellationToken));
    app.MapPost(
        "/internal/realtime/alerts",
        (AlertRaised message, IRealtimeNotifier notifier, CancellationToken cancellationToken) =>
            notifier.BroadcastAlertAsync(message, cancellationToken));
}

app.UseCors("Dashboard");
app.MapControllers();
app.MapHub<DashboardHub>("/hubs/dashboard");
app.MapGet("/", () => Results.Ok(new
{
    service = "TfL Analytics API",
    status = "running",
    endpoints = new
    {
        health = "/health/live",
        stations = "/api/stations",
        arrivals = "/api/stations/{stationId}/arrivals",
        lineStatus = "/api/lines/status",
        alerts = "/api/alerts",
        dashboard = "/api/dashboard/summary",
        signalR = "/hubs/dashboard"
    }
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

app.Run();
