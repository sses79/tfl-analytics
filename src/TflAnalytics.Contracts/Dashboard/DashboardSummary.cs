namespace TflAnalytics.Contracts.Dashboard;

public sealed record DashboardSummary(
    int LinesMonitored,
    int LinesDisrupted,
    int StationsMonitored,
    int RecentAlertCount,
    DateTimeOffset? LastEventUtc);
