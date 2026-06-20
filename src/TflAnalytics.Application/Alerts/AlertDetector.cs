using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Events;

namespace TflAnalytics.Application.Alerts;

public sealed class AlertDetector : IAlertDetector
{
    private readonly IObservationHistory _history;
    private readonly AlertOptions _options;
    private readonly TimeProvider _timeProvider;

    public AlertDetector(
        IObservationHistory history,
        AlertOptions options,
        TimeProvider timeProvider)
    {
        _history = history;
        _options = options;
        _timeProvider = timeProvider;
    }

    public async Task<AlertCandidate?> DetectArrivalAsync(
        EventEnvelope<ArrivalPredictionObserved> envelope,
        CancellationToken cancellationToken = default)
    {
        if (envelope.Payload.ExpectedArrivalUtc is null)
        {
            return null;
        }

        var previous = await _history.GetPreviousArrivalAsync(
            envelope,
            cancellationToken);
        if (previous?.ExpectedArrivalUtc is null)
        {
            return null;
        }

        var slippageSeconds = (int)Math.Round(
            (envelope.Payload.ExpectedArrivalUtc.Value
                - previous.ExpectedArrivalUtc.Value).TotalSeconds,
            MidpointRounding.AwayFromZero);
        if (slippageSeconds <= _options.ArrivalSlippageThresholdSeconds)
        {
            return null;
        }

        if (previous.PriorExpectedArrivalUtc is not null)
        {
            var previousSlippageSeconds = (int)Math.Round(
                (previous.ExpectedArrivalUtc.Value
                    - previous.PriorExpectedArrivalUtc.Value).TotalSeconds,
                MidpointRounding.AwayFromZero);
            if (previousSlippageSeconds > _options.ArrivalSlippageThresholdSeconds)
            {
                // Already over threshold last poll too - this is the same
                // ongoing delay, not a new crossing, so don't re-alert.
                return null;
            }
        }

        return Create(
            AlertRuleTypes.ArrivalPredictionSlippage,
            envelope.EventId,
            envelope.ObservedAtUtc,
            envelope.StationId,
            envelope.LineId,
            envelope.Payload.VehicleId,
            "Arrival prediction delayed",
            $"Arrival prediction slipped by {slippageSeconds} seconds.",
            previous.ExpectedArrivalUtc.Value.ToString("O"),
            envelope.Payload.ExpectedArrivalUtc.Value.ToString("O"));
    }

    public async Task<AlertCandidate?> DetectLineStatusAsync(
        EventEnvelope<LineStatusObserved> envelope,
        CancellationToken cancellationToken = default)
    {
        var previous = await _history.GetPreviousLineStatusAsync(
            envelope,
            cancellationToken);
        if (previous is null
            || previous.StatusSeverity != _options.GoodServiceSeverity
            || envelope.Payload.StatusSeverity >= _options.GoodServiceSeverity)
        {
            return null;
        }

        return Create(
            AlertRuleTypes.LineStatusDisruption,
            envelope.EventId,
            envelope.ObservedAtUtc,
            null,
            envelope.LineId,
            null,
            "Line status disrupted",
            envelope.Payload.Reason
                ?? $"{envelope.Payload.LineName} changed to "
                    + envelope.Payload.StatusSeverityDescription + ".",
            previous.StatusSeverityDescription,
            envelope.Payload.StatusSeverityDescription);
    }

    private AlertCandidate Create(
        string ruleType,
        string sourceEventId,
        DateTimeOffset observedAtUtc,
        string? stationId,
        string? lineId,
        string? vehicleId,
        string title,
        string description,
        string previousValue,
        string currentValue)
    {
        var identity = string.Join(
            "|",
            ruleType,
            sourceEventId,
            stationId,
            lineId,
            vehicleId);
        var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLower(CultureInfo.InvariantCulture);

        return new AlertCandidate(
            hash,
            ruleType,
            sourceEventId,
            _timeProvider.GetUtcNow(),
            observedAtUtc,
            stationId,
            lineId,
            vehicleId,
            title,
            description,
            previousValue,
            currentValue);
    }
}
