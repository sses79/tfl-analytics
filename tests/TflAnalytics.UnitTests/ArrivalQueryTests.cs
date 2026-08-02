using TflAnalytics.Contracts.Events;
using TflAnalytics.Infrastructure.Processing;

namespace TflAnalytics.UnitTests;

public sealed class ArrivalQueryTests
{
    private static readonly DateTimeOffset LatestObservation =
        DateTimeOffset.Parse("2026-08-02T12:05:00Z");

    [Fact]
    public void LatestSnapshotExcludesStaleObservationsAndDuplicateTrains()
    {
        var documents = new[]
        {
            CreateDocument("100", LatestObservation.AddMinutes(-5), 30),
            CreateDocument("100", LatestObservation, 120),
            CreateDocument("100", LatestObservation, 125),
            CreateDocument("200", LatestObservation, 60)
        };

        var result = CosmosEventRepository.SelectLatestArrivalSnapshot(documents, 20);

        Assert.Equal(2, result.Count);
        Assert.All(result, arrival => Assert.Equal(LatestObservation, arrival.ObservedAtUtc));
        Assert.Equal([60, 120], result.Select(arrival => arrival.SecondsToStation));
    }

    [Fact]
    public void LatestSnapshotPreservesPredictionsAndAppliesCountAfterDeduplication()
    {
        var documents = new[]
        {
            CreateDocument("100", LatestObservation, 180),
            CreateDocument("200", LatestObservation, 60),
            CreateDocument("300", LatestObservation, 120)
        };

        var result = CosmosEventRepository.SelectLatestArrivalSnapshot(documents, 2);

        Assert.Equal([60, 120], result.Select(arrival => arrival.SecondsToStation));
    }

    [Fact]
    public void LatestSnapshotReturnsEmptyForNonPositiveCount()
    {
        var result = CosmosEventRepository.SelectLatestArrivalSnapshot(
            [CreateDocument("100", LatestObservation, 60)],
            0);

        Assert.Empty(result);
    }

    private static CosmosEventRepository.ArrivalQueryDocument CreateDocument(
        string vehicleId,
        DateTimeOffset observedAtUtc,
        int secondsToStation) =>
        new(
            "victoria",
            observedAtUtc,
            new ArrivalPredictionObserved(
                vehicleId,
                "940GZZLUVIC",
                "Victoria Underground Station",
                "victoria",
                "Victoria",
                "Walthamstow Central Underground Station",
                "Northbound - Platform 3",
                "inbound",
                observedAtUtc.AddSeconds(secondsToStation),
                secondsToStation,
                observedAtUtc));
}
