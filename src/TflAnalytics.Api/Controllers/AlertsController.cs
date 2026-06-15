using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Alerts;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alertRepository;

    public AlertsController(IAlertRepository alertRepository) =>
        _alertRepository = alertRepository;

    [HttpGet]
    public Task<IReadOnlyList<AlertSummary>> GetAlerts(
        [FromQuery] int count = 50,
        CancellationToken cancellationToken = default) =>
        _alertRepository.GetRecentAlertsAsync(count, cancellationToken);
}
