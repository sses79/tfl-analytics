namespace TflAnalytics.Application.Alerts;

public sealed class AlertOptions
{
    public const string SectionName = "Alerts";

    public int ArrivalSlippageThresholdSeconds { get; set; } = 1200;

    public int GoodServiceSeverity { get; set; } = 10;
}
