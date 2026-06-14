using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class ArrivalsController : ControllerBase
{
    private readonly IEventRepository _eventRepository;

    public ArrivalsController(IEventRepository eventRepository) =>
        _eventRepository = eventRepository;

    [HttpGet("{stationId}/arrivals")]
    public Task<IReadOnlyList<ArrivalSummary>> GetArrivals(
        string stationId,
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default) =>
        _eventRepository.GetRecentArrivalsAsync(stationId, count, cancellationToken);
}
