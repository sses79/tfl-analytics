using TflAnalytics.Infrastructure;

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
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Dashboard");
app.MapControllers();
app.MapGet("/", () => Results.Ok(new
{
    service = "TfL Analytics API",
    status = "running",
    endpoints = new
    {
        health = "/health/live",
        lineStatusExample = "/api/tfl/line-status/victoria,circle"
    }
}));
app.MapGet("/health/live", () => Results.Ok(new { status = "healthy" }));

app.Run();
