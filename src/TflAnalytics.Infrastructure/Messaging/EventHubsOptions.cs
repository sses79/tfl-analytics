namespace TflAnalytics.Infrastructure.Messaging;

public sealed class EventHubsOptions
{
    public const string SectionName = "EventHubs";

    public string? ConnectionString { get; set; }

    public string? FullyQualifiedNamespace { get; set; }

    public string EventHubName { get; set; } = "tfl-events";
}
