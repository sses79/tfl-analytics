namespace TflAnalytics.Infrastructure.Realtime;

public sealed class SignalROptions
{
    public const string SectionName = "SignalR";

    public string? Endpoint { get; set; }

    public string? RelayBaseUrl { get; set; }
}
