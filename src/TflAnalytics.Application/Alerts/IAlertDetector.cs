using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Alerts;

public interface IAlertDetector
{
    Task<AlertCandidate?> DetectArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> envelope,
        CancellationToken cancellationToken = default);

    Task<AlertCandidate?> DetectLineStatusAsync(
        EventEnvelope<LineStatusObserved> envelope,
        CancellationToken cancellationToken = default);
}
