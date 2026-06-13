using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TflAnalytics.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.AddFilter("Azure.Core", LogLevel.Warning);
builder.Logging.AddFilter("Azure.Messaging.EventHubs", LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProcessingInfrastructure(builder.Configuration);

builder.Build().Run();
