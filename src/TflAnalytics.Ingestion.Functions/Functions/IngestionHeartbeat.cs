using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class IngestionHeartbeat
{
    private readonly ILogger<IngestionHeartbeat> _logger;

    public IngestionHeartbeat(ILogger<IngestionHeartbeat> logger)
    {
        _logger = logger;
    }

    [Function(nameof(IngestionHeartbeat))]
    public void Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer)
    {
        _logger.LogInformation(
            "Ingestion host heartbeat at {TimestampUtc}; next run {NextRunUtc}.",
            DateTimeOffset.UtcNow,
            timer.ScheduleStatus?.Next);
    }
}
