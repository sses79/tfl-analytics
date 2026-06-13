using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class PollArrivals
{
    private readonly IIngestionPoller _poller;
    private readonly ILogger<PollArrivals> _logger;

    public PollArrivals(
        IIngestionPoller poller,
        ILogger<PollArrivals> logger)
    {
        _poller = poller;
        _logger = logger;
    }

    [Function(nameof(PollArrivals))]
    public async Task Run(
        [TimerTrigger("%IngestionArrivalsSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var published = await _poller.PollArrivalsAsync(cancellationToken);

        _logger.LogInformation(
            "Published {EventCount} arrival observation events. Past due: {IsPastDue}.",
            published,
            timer.IsPastDue);
    }
}
