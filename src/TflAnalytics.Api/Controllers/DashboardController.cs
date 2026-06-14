using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Ingestion;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IngestionOptions _ingestionOptions;

    public DashboardController(
        IEventRepository eventRepository,
        IAlertRepository alertRepository,
        IngestionOptions ingestionOptions)
    {
        _eventRepository = eventRepository;
        _alertRepository = alertRepository;
        _ingestionOptions = ingestionOptions;
    }

    [HttpGet("summary")]
    public async Task<DashboardSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        var lineStatusTask = _eventRepository.GetCurrentLineStatusAsync(cancellationToken);
        var alertsTask = _alertRepository.GetRecentAlertsAsync(50, cancellationToken);

        await Task.WhenAll(lineStatusTask, alertsTask);

        var lineStatuses = lineStatusTask.Result;
        var alerts = alertsTask.Result;

        var disrupted = lineStatuses.Count(s => s.StatusSeverity < 10);
        var lastEvent = lineStatuses.Count > 0
            ? lineStatuses.Max(s => s.ObservedAtUtc)
            : (DateTimeOffset?)null;

        return new DashboardSummary(
            LinesMonitored: lineStatuses.Count,
            LinesDisrupted: disrupted,
            StationsMonitored: _ingestionOptions.StationIds.Length,
            RecentAlertCount: alerts.Count,
            LastEventUtc: lastEvent);
    }
}
