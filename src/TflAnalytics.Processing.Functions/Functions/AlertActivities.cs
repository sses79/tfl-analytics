using Microsoft.Azure.Functions.Worker;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Alerts;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class AlertActivities
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAuditRepository _auditRepository;
    private readonly INotificationSender _notificationSender;

    public AlertActivities(
        IAlertRepository alertRepository,
        IAuditRepository auditRepository,
        INotificationSender notificationSender)
    {
        _alertRepository = alertRepository;
        _auditRepository = auditRepository;
        _notificationSender = notificationSender;
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
}
