using TflAnalytics.Application.Tfl;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Tfl;
using TflAnalytics.Infrastructure.Tfl;

namespace TflAnalytics.UnitTests;

public sealed class CachedJourneyPlannerTests
{
    [Fact]
    public async Task ReusesIdenticalJourneyWithinOneMinute()
    {
        var client = new StubClient();
        var planner = new CachedJourneyPlanner(client, new StubDepartureBoards(), TimeProvider.System);

        await planner.GetAsync("from", "to", "leasttime", ["stepFreeToPlatform"]);
        await planner.GetAsync("from", "to", "leasttime", ["stepFreeToPlatform"]);

        Assert.Equal(1, client.JourneyCalls);
    }

    [Fact]
    public async Task RemovesPassengerEquivalentJourneysAndDeduplicatesDisruptions()
    {
        var direct = new Journey(
            51,
            DateTimeOffset.Parse("2026-08-04T10:24:00Z"),
            DateTimeOffset.Parse("2026-08-04T11:15:00Z"),
            [
                Walking("King's Cross", "King's Cross"),
                Tube("King's Cross", "Barking", "Hammersmith & City", "No Step Free Access"),
                Walking("Barking", "Barking")
            ]);
        var client = new StubClient(new JourneyPlan([direct, direct]));
        var planner = new CachedJourneyPlanner(client, new StubDepartureBoards(), TimeProvider.System);

        var plan = await planner.GetAsync("from", "to", "leastinterchange", ["stepFreeToPlatform"]);

        var journey = Assert.Single(plan.Journeys);
        Assert.Equal(1, plan.DuplicateCountRemoved);
        Assert.Equal(0, journey.ChangeCount);
        Assert.Equal("Does not meet selected access need", journey.AccessibilitySummary);
        Assert.Single(journey.Disruptions);
        Assert.Contains("Recommended", journey.Labels);
    }

    [Fact]
    public async Task KeepsEquivalentJourneysWhenDepartureTimeIsUnknown()
    {
        var first = new Journey(20, null, null, [Tube("A", "B", "District", "")]);
        var second = new Journey(22, null, null, [Tube("A", "B", "District", "")]);
        var planner = new CachedJourneyPlanner(new StubClient(new JourneyPlan([first, second])), new StubDepartureBoards(), TimeProvider.System);

        var plan = await planner.GetAsync("from", "to", "leasttime", []);

        Assert.Equal(2, plan.Journeys.Count);
        Assert.Equal(0, plan.DuplicateCountRemoved);
    }

    [Fact]
    public async Task DetectsAlternativeLiftAccessibilityWording()
    {
        var journey = new Journey(20, DateTimeOffset.Parse("2026-08-04T10:00:00Z"), null,
            [Tube("A", "B", "District", "Access lift unavailable")]);
        var planner = new CachedJourneyPlanner(new StubClient(new JourneyPlan([journey])), new StubDepartureBoards(), TimeProvider.System);

        var plan = await planner.GetAsync("from", "to", "leasttime", ["stepFreeToPlatform"]);

        Assert.Equal("Does not meet selected access need", Assert.Single(plan.Journeys).AccessibilitySummary);
    }

    [Fact]
    public async Task NormalizesDeduplicatesAndRanksStationSearchForOrigin()
    {
        var client = new StubClient(search: new StopPointSearchResult([
            new("940GZZLUBKE", "Barkingside Underground Station", ["tube"]),
            new("940GZZLUBKG", "Barking Underground Station", ["tube"]),
            new("940GZZLUBKG", "Barking Underground Station", ["tube"]),
            new("490G00000001", "Barking Bus Station", ["bus"])
        ]));
        var planner = new CachedJourneyPlanner(
            client,
            new StubDepartureBoards([new("940GZZLUBKG", "Barking", ["district"])]),
            TimeProvider.System);

        var result = await planner.SearchStationsAsync("Barking", "origin");

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal("Barking", result.Matches[0].DisplayName);
        Assert.True(result.Matches[0].IsDirect);
        Assert.Equal(["district"], result.Matches[0].Lines);
        Assert.Equal("Barkingside", result.Matches[1].DisplayName);
    }

    private static JourneyLeg Walking(string from, string to) => new(
        2, new(null, from), new(null, to), new("Transfer", null), new("walking", "walking"), [], []);

    private static JourneyLeg Tube(string from, string to, string line, string disruption) => new(
        47, new("from", from), new("to", to), new($"{line} line to {to}", null), new("tube", "tube"),
        [new(line, [to])], [new("accessibility", disruption), new("accessibility", disruption)]);

    private sealed class StubClient : ITflApiClient
    {
        private readonly JourneyPlan _plan;
        private readonly StopPointSearchResult _search;
        public StubClient(JourneyPlan? plan = null, StopPointSearchResult? search = null)
        {
            _plan = plan ?? new JourneyPlan([]);
            _search = search ?? new StopPointSearchResult([]);
        }
        public int JourneyCalls { get; private set; }

        public Task<JourneyPlan> GetJourneyPlanAsync(
            string fromStationId,
            string toStationId,
            string journeyPreference,
            IReadOnlyList<string> accessibilityPreferences,
            CancellationToken cancellationToken = default)
        {
            JourneyCalls++;
            return Task.FromResult(_plan);
        }

        public Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(string stationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StopPoint> GetStopPointAsync(string stationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StopPointSearchResult> SearchStopPointsAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult(_search);
        public Task<IReadOnlyList<Line>> GetLineStatusAsync(IEnumerable<string> lineIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RouteSequence> GetRouteSequenceAsync(string lineId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubDepartureBoards : IDepartureBoardService
    {
        private readonly IReadOnlyList<DestinationOption> _destinations;
        public StubDepartureBoards(IReadOnlyList<DestinationOption>? destinations = null) => _destinations = destinations ?? [];
        public Task<IReadOnlyList<DestinationOption>> GetDestinationsAsync(string stationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_destinations);
        public Task<DepartureBoard> GetAsync(string stationId, string? destinationStationId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DepartureBoard(stationId, null, null, true, _destinations, [], [], []));
    }
}
