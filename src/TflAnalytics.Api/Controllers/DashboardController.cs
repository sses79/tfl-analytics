using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Application.Ingestion;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private const string RecentAlertCountCacheKey = "dashboard:recentAlertCount";
    private static readonly TimeSpan RecentAlertCountCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IEventRepository _eventRepository;
    private readonly IAlertRepository _alertRepository;
    private readonly IngestionOptions _ingestionOptions;
    private readonly IMemoryCache _cache;

    public DashboardController(
        IEventRepository eventRepository,
        IAlertRepository alertRepository,
        IngestionOptions ingestionOptions,
        IMemoryCache cache)
    {
        _eventRepository = eventRepository;
        _alertRepository = alertRepository;
        _ingestionOptions = ingestionOptions;
        _cache = cache;
    }

    [HttpGet("summary")]
    public async Task<DashboardSummary> GetSummary(CancellationToken cancellationToken = default)
    {
        var lineStatusTask = _eventRepository.GetCurrentLineStatusAsync(cancellationToken);
        var alertCountTask = GetRecentAlertCountAsync(cancellationToken);

        await Task.WhenAll(lineStatusTask, alertCountTask);

        var lineStatuses = lineStatusTask.Result;

        var disrupted = lineStatuses.Count(s => s.StatusSeverity < 10);
        var lastEvent = lineStatuses.Count > 0
            ? lineStatuses.Max(s => s.ObservedAtUtc)
            : (DateTimeOffset?)null;

        return new DashboardSummary(
            LinesMonitored: lineStatuses.Count,
            LinesDisrupted: disrupted,
            StationsMonitored: _ingestionOptions.StationIds.Length,
            RecentAlertCount: alertCountTask.Result,
            LastEventUtc: lastEvent);
    }

    // The dashboard re-fetches this summary on every SignalR push (arrivals/line-status update
    // every few minutes per station/line), but the alert count rarely changes between pushes.
    // Caching also avoids repeating the same Table Storage query on every realtime update.
    private Task<int> GetRecentAlertCountAsync(CancellationToken cancellationToken) =>
        _cache.GetOrCreateAsync(RecentAlertCountCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = RecentAlertCountCacheDuration;
            var alerts = await _alertRepository.GetRecentAlertsAsync(50, cancellationToken);
            return alerts.Count;
        })!;
}
