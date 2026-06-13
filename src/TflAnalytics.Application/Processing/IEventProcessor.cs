using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Application.Processing;

public interface IEventProcessor
{
    Task<ProcessingResult> ProcessAsync(
        ProcessingMessage message,
        CancellationToken cancellationToken = default);
}
