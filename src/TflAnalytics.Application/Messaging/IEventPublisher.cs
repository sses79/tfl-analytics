using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Messaging;

public interface IEventPublisher
{
    Task PublishAsync<TPayload>(
        EventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}
