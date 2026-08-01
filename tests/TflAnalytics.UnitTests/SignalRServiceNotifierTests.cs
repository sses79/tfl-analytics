using TflAnalytics.Infrastructure.Realtime;

namespace TflAnalytics.UnitTests;

public sealed class SignalRServiceNotifierTests
{
    [Fact]
    public void BroadcastUrlUsesTheLowercaseNegotiatedHubName()
    {
        var url = SignalRServiceNotifier.BuildBroadcastUrl(
            "https://example.service.signalr.net/");

        Assert.Equal(
            "https://example.service.signalr.net/api/v1/hubs/dashboardhub",
            url);
    }
}
