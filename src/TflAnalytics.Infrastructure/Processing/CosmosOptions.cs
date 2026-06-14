namespace TflAnalytics.Infrastructure.Processing;

public sealed class CosmosOptions
{
    public const string SectionName = "Cosmos";

    public string? ConnectionString { get; set; }

    public string? Endpoint { get; set; }

    public string DatabaseName { get; set; } = "tfl-analytics";

    public string LiveEventsContainerName { get; set; } = "live-events";

    public string LineStatusContainerName { get; set; } = "line-status";

    public bool Initialize { get; set; }

    public bool DisableServerCertificateValidation { get; set; }

    public int DefaultTtlSeconds { get; set; } = 604800;
}
