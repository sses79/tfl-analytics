using TflAnalytics.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "TfL Analytics API",
    status = "running",
    endpoints = new
    {
        health = "/health/live",
        openApi = "/openapi/v1.json",
        lineStatusExample = "/api/tfl/line-status/victoria,circle"
    }
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

app.Run();
