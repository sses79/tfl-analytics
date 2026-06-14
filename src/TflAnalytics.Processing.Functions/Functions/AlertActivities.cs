using Microsoft.Azure.Functions.Worker;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Alerts;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class AlertActivities
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly INotificationSender _notificationSender;
    private readonly IRealtimeNotifier _realtimeNotifier;

    public AlertActivities(
        IAlertRepository alertRepository,
        IAuditRepository auditRepository,
        INotificationSender notificationSender,
        IRealtimeNotifier realtimeNotifier)
    {
        _alertRepository = alertRepository;
        _auditRepository = auditRepository;
        _notificationSender = notificationSender;
        _realtimeNotifier = realtimeNotifier;
    }

    [Function(nameof(PersistAlert))]
    public async Task<bool> PersistAlert(
        [ActivityTrigger] AlertCandidate alert,
        CancellationToken cancellationToken)
    {
        await _alertRepository.EnsureInitializedAsync(cancellationToken);
        return await _alertRepository.CreateAsync(alert, cancellationToken);
    }

    [Function(nameof(WriteAlertAudit))]
    public Task WriteAlertAudit(
        [ActivityTrigger] AlertCandidate alert,
        CancellationToken cancellationToken) =>
        _auditRepository.WriteAlertRaisedAsync(alert, cancellationToken);

    [Function(nameof(SendMockAlertNotification))]
    public Task SendMockAlertNotification(
        [ActivityTrigger] AlertCandidate alert,
        CancellationToken cancellationToken) =>
        _notificationSender.SendAsync(alert, cancellationToken);

    [Function(nameof(BroadcastAlert))]
    public Task BroadcastAlert(
        [ActivityTrigger] AlertCandidate alert,
        CancellationToken cancellationToken) =>
        _realtimeNotifier.BroadcastAlertAsync(
            new AlertRaised(
                alert.AlertId,
                alert.RuleType,
                alert.StationId,
                alert.LineId,
                alert.Title,
                alert.Description,
                alert.PreviousValue,
                alert.CurrentValue,
                alert.DetectedAtUtc),
            cancellationToken);
}
