namespace TflAnalytics.Infrastructure.Processing;

public sealed class ProcessingStorageOptions
{
    public const string SectionName = "ProcessingStorage";

    public string? ConnectionString { get; set; }

    public string? AccountName { get; set; }

    public string RawContainerName { get; set; } = "raw";

    public string QueueName { get; set; } = "processing";

    public bool Initialize { get; set; }
}
