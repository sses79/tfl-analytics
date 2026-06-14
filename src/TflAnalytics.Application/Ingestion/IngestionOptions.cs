namespace TflAnalytics.Application.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    public string[] StationIds { get; set; } = [];

    public string[] LineIds { get; set; } = [];
}
