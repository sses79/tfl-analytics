namespace TflAnalytics.Infrastructure.Ingestion;

public sealed class DemoPollingStorageOptions
{
    public const string SectionName = "DemoPollingStorage";
    public string? ConnectionString { get; set; }
    public string? AccountName { get; set; }
    public string ContainerName { get; set; } = "runtime-control";
    public string BlobName { get; set; } = "arrival-demo-polling.json";
}
