namespace TflAnalytics.Infrastructure.Alerts;

public sealed class AlertStorageOptions
{
    public const string SectionName = "AlertStorage";

    public string? ConnectionString { get; set; }

    public string? ServerFqdn { get; set; }

    public string DatabaseName { get; set; } = "tfl-analytics";

    public bool Initialize { get; set; } = true;
}
