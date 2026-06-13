using TflAnalytics.Application.Messaging;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Ingestion;

public sealed class IngestionPoller : IIngestionPoller
{
    private static readonly TimeSpan ArrivalObservationWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan LineStatusObservationWindow = TimeSpan.FromMinutes(2);

    private readonly ITflApiClient _tflApiClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly IngestionOptions _options;
    private readonly TimeProvider _timeProvider;

    public IngestionPoller(
        ITflApiClient tflApiClient,
        IEventPublisher eventPublisher,
        IngestionOptions options,
        TimeProvider timeProvider)
    {
        _tflApiClient = tflApiClient;
        _eventPublisher = eventPublisher;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<int> PollArrivalsAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;

        foreach (var stationId in Normalize(_options.StationIds))
        {
            var observedAtUtc = _timeProvider.GetUtcNow();
            var arrivals = await _tflApiClient.GetArrivalsAsync(stationId, cancellationToken);

            foreach (var arrival in arrivals)
            {
                var payload = new ArrivalPredictionObserved(
                    arrival.VehicleId,
                    stationId,
                    arrival.StationName,
                    arrival.LineId,
                    arrival.LineName,
                    arrival.DestinationName,
                    arrival.PlatformName,
                    arrival.Direction,
                    arrival.ExpectedArrival,
                    arrival.TimeToStation,
                    arrival.Timestamp);

                var eventId = EventIdFactory.Create(
                    EventTypes.ArrivalPredictionObserved,
                    observedAtUtc,
                    ArrivalObservationWindow,
                    stationId,
                    arrival.VehicleId ?? arrival.Id,
                    arrival.ExpectedArrival?.ToUniversalTime().ToString("O"));

                await _eventPublisher.PublishAsync(
                    new EventEnvelope<ArrivalPredictionObserved>(
                        eventId,
                        EventTypes.ArrivalPredictionObserved,
                        "TfL",
                        observedAtUtc,
                        stationId,
                        arrival.LineId,
                        1,
                        payload),
                    cancellationToken);

                published++;
            }
        }

        return published;
    }

    public async Task<int> PollLineStatusAsync(CancellationToken cancellationToken = default)
    {
        var lineIds = Normalize(_options.LineIds);
        if (lineIds.Length == 0)
        {
            return 0;
        }

        var observedAtUtc = _timeProvider.GetUtcNow();
        var lines = await _tflApiClient.GetLineStatusAsync(lineIds, cancellationToken);
        var published = 0;

        foreach (var line in lines)
        {
            foreach (var status in line.LineStatuses)
            {
                var payload = new LineStatusObserved(
                    line.Id,
                    line.Name,
                    status.StatusSeverity,
                    status.StatusSeverityDescription,
                    status.Reason);

                var eventId = EventIdFactory.Create(
                    EventTypes.LineStatusObserved,
                    observedAtUtc,
                    LineStatusObservationWindow,
                    line.Id,
                    status.StatusSeverity.ToString(),
                    status.Reason);

                await _eventPublisher.PublishAsync(
                    new EventEnvelope<LineStatusObserved>(
                        eventId,
                        EventTypes.LineStatusObserved,
                        "TfL",
                        observedAtUtc,
                        null,
                        line.Id,
                        1,
                        payload),
                    cancellationToken);

                published++;
            }
        }

        return published;
    }

    private static string[] Normalize(IEnumerable<string> values) =>
        values
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
