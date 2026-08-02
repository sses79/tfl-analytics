using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class PollArrivals
{
    private readonly IIngestionPoller _poller;
    private readonly IArrivalDemoPollingControl _pollingControl;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PollArrivals> _logger;

    public PollArrivals(
        IIngestionPoller poller,
        IArrivalDemoPollingControl pollingControl,
        TimeProvider timeProvider,
        ILogger<PollArrivals> logger)
    {
        _poller = poller;
        _pollingControl = pollingControl;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [Function(nameof(PollArrivals))]
    public async Task Run(
        [TimerTrigger("%IngestionArrivalsSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var decision = await _pollingControl.EvaluateAsync(
            _timeProvider.GetUtcNow(),
            cancellationToken);
        if (decision.Reason.StartsWith("control-failure", StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Demo polling control was unavailable; using five-minute baseline. "
                + "Decision: {Reason}.",
                decision.Reason);
        }
        if (!decision.ShouldPoll)
        {
            _logger.LogInformation(
                "Skipped arrival poll. Reason: {Reason}.",
                decision.Reason);
            return;
        }

        var published = await _poller.PollArrivalsAsync(cancellationToken);

        _logger.LogInformation(
            "Published {EventCount} arrival observation events. Demo boost: {DemoBoostActive}. "
            + "Boost expiry: {DemoBoostExpiresAtUtc}. Past due: {IsPastDue}.",
            published,
            decision.DemoBoostActive,
            decision.DemoBoostExpiresAtUtc,
            timer.IsPastDue);
    }
}
