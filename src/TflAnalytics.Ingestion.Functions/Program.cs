using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TflAnalytics.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
