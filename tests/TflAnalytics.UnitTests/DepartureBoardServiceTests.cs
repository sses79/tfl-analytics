using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Events;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.UnitTests;

public sealed class DepartureBoardServiceTests
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-08-02T18:00:00Z");

    [Fact]
    public async Task RecommendsDirectRouteWhenDestinationFollowsOrigin()
    {
        var service = CreateService([CreateArrival("940GZZLUWWL")]);

        var board = await service.GetAsync("940GZZLUVIC", "940GZZLUKSX");

        var recommendation = Assert.Single(board.Recommendations);
        Assert.Equal("victoria", recommendation.LineId);
        Assert.Equal("inbound", recommendation.Direction);
        Assert.Equal(5, recommendation.StopsUntilDestination);
        Assert.True(Assert.Single(board.Platforms).Trains[0].ServesSelectedDestination);
        Assert.False(board.IsStale);
    }

    [Fact]
    public async Task DoesNotRecommendReverseDirectionOrTrainTerminatingEarly()
    {
        var service = CreateService([CreateArrival("940GZZLUOXC")]);

        var board = await service.GetAsync("940GZZLUVIC", "940GZZLUKSX");

        Assert.Single(board.Recommendations);
        Assert.False(Assert.Single(board.Platforms).Trains[0].ServesSelectedDestination);
    }

    [Fact]
    public async Task DestinationsOnlyContainStationsAfterTheOrigin()
    {
        var service = CreateService([CreateArrival("940GZZLUWWL")]);

        var board = await service.GetAsync("940GZZLUVIC");

        Assert.DoesNotContain(board.Destinations, item => item.StationId == "940GZZLUBXN");
        Assert.Contains(board.Destinations, item => item.StationId == "940GZZLUKSX");
    }

    private static DepartureBoardService CreateService(IReadOnlyList<ArrivalSummary> arrivals) =>
        new(
            new StubRepository(arrivals),
            new StubRouteProvider(CreateRoute()),
            new FixedTimeProvider(Now));

    private static ArrivalSummary CreateArrival(string destinationStationId) =>
        new(
            "victoria",
            "Victoria",
            "Walthamstow Central",
            "Northbound - Platform 3",
            "inbound",
            Now.AddMinutes(2),
            120,
            Now,
            "prediction-1",
            "vehicle-1",
            "940GZZLUVIC",
            "Victoria Underground Station",
            destinationStationId,
            "Walthamstow Central",
            "Approaching Victoria");

    private static RouteSequence CreateRoute() =>
        new(
            "victoria",
            "Victoria",
            "all",
            [
                new StopPointSequence(
                    "victoria",
                    "Victoria",
                    "inbound",
                    0,
                    [
                        Stop("940GZZLUBXN", "Brixton"),
                        Stop("940GZZLUVIC", "Victoria"),
                        Stop("940GZZLUGPK", "Green Park"),
                        Stop("940GZZLUOXC", "Oxford Circus"),
                        Stop("940GZZLUWRR", "Warren Street"),
                        Stop("940GZZLUEUS", "Euston"),
                        Stop("940GZZLUKSX", "King's Cross St. Pancras"),
                        Stop("940GZZLUWWL", "Walthamstow Central")
                    ],
                    "Regular")
            ]);

    private static MatchedStop Stop(string id, string name) =>
        new(id, name, id, null, "inbound", null);

    private sealed class StubRouteProvider : IRouteSequenceProvider
    {
        private readonly RouteSequence _route;

        public StubRouteProvider(RouteSequence route) => _route = route;

        public Task<RouteSequence> GetAsync(
            string lineId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_route);
    }

    private sealed class StubRepository : IEventRepository
    {
        private readonly IReadOnlyList<ArrivalSummary> _arrivals;

        public StubRepository(IReadOnlyList<ArrivalSummary> arrivals) =>
            _arrivals = arrivals;

        public Task<IReadOnlyList<ArrivalSummary>> GetRecentArrivalsAsync(
            string stationId,
            int maxCount = 20,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_arrivals);

        public Task<bool> CreateArrivalAsync(
            EventEnvelope<ArrivalPredictionObserved> envelope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> CreateLineStatusAsync(
            EventEnvelope<LineStatusObserved> envelope,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LineStatusSummary>> GetCurrentLineStatusAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;
        public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
