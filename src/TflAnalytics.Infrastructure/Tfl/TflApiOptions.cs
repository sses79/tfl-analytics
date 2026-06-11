namespace TflAnalytics.Infrastructure.Tfl;

public sealed class TflApiOptions
{
    public const string SectionName = "TflApi";

    public string BaseUrl { get; set; } = "https://api.tfl.gov.uk/";

    public string? AppKey { get; set; }
}
