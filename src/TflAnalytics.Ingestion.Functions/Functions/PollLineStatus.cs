using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class PollLineStatus
{
    private readonly IIngestionPoller _poller;
    private readonly ILogger<PollLineStatus> _logger;

    public PollLineStatus(
        IIngestionPoller poller,
        ILogger<PollLineStatus> logger)
    {
        _poller = poller;
        _logger = logger;
    }

    [Function(nameof(PollLineStatus))]
    public async Task Run(
        [TimerTrigger("%IngestionLineStatusSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var published = await _poller.PollLineStatusAsync(cancellationToken);

        _logger.LogInformation(
            "Published {EventCount} line-status observation events. Past due: {IsPastDue}.",
            published,
            timer.IsPastDue);
    }
}
