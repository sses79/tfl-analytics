using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;
using TflAnalytics.Infrastructure.Tfl;

namespace TflAnalytics.UnitTests;

public sealed class CachedJourneyPlannerTests
{
    [Fact]
    public async Task ReusesIdenticalJourneyWithinOneMinute()
    {
        var client = new StubClient();
        var planner = new CachedJourneyPlanner(client, TimeProvider.System);

        await planner.GetAsync("from", "to", "leasttime", ["stepFreeToPlatform"]);
        await planner.GetAsync("from", "to", "leasttime", ["stepFreeToPlatform"]);

        Assert.Equal(1, client.JourneyCalls);
    }

    private sealed class StubClient : ITflApiClient
    {
        public int JourneyCalls { get; private set; }

        public Task<JourneyPlan> GetJourneyPlanAsync(
            string fromStationId,
            string toStationId,
            string journeyPreference,
            IReadOnlyList<string> accessibilityPreferences,
            CancellationToken cancellationToken = default)
        {
            JourneyCalls++;
            return Task.FromResult(new JourneyPlan([]));
        }

        public Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(string stationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StopPoint> GetStopPointAsync(string stationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StopPointSearchResult> SearchStopPointsAsync(string query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Line>> GetLineStatusAsync(IEnumerable<string> lineIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteSequence> GetRouteSequenceAsync(string lineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
