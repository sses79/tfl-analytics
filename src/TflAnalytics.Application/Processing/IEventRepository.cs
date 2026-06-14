using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Processing;

public interface IEventRepository
{
    Task<bool> CreateArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> envelope,
        CancellationToken cancellationToken = default);

    Task<bool> CreateLineStatusAsync(
        EventEnvelope<LineStatusObserved> envelope,
        CancellationToken cancellationToken = default);
}
