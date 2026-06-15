using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.AddFilter("Azure.Core", LogLevel.Warning);
builder.Logging.AddFilter("Azure.Messaging.EventHubs", LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProcessingInfrastructure(builder.Configuration);

var host = builder.Build();

try
{
    await host.Services
        .GetRequiredService<IAlertRepository>()
        .EnsureInitializedAsync();
}
catch (Exception ex)
{
    host.Services
        .GetRequiredService<ILogger<Program>>()
        .LogError(ex, "SQL initialization failed at startup; will retry on first write.");
}

await host.RunAsync();
