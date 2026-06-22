using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Alerts;

public interface IObservationHistory
{
    Task<ArrivalObservation?> GetPreviousArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> current,
        CancellationToken cancellationToken = default);

    Task<LineStatusObservation?> GetPreviousLineStatusAsync(
        EventEnvelope<LineStatusObserved> current,
        CancellationToken cancellationToken = default);
}

public sealed record ArrivalObservation(
    string EventId,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset? ExpectedArrivalUtc,
    DateTimeOffset? PriorExpectedArrivalUtc = null,
    string? Direction = null);

public sealed record LineStatusObservation(
    string EventId,
    DateTimeOffset ObservedAtUtc,
    int StatusSeverity,
    string StatusSeverityDescription);
