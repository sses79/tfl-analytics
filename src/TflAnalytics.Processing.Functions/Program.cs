using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using TflAnalytics.Infrastructure;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Build().Run();
