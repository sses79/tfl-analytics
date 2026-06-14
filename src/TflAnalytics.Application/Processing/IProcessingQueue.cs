using TflAnalytics.Contracts.Processing;

namespace TflAnalytics.Application.Processing;

public interface IProcessingQueue
{
    Task EnqueueAsync(
        ProcessingMessage message,
        CancellationToken cancellationToken = default);
}
