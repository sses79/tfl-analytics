using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/lines")]
public sealed class LineStatusController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IRouteSequenceProvider _routeSequenceProvider;

    public LineStatusController(
        IEventRepository eventRepository,
        IRouteSequenceProvider routeSequenceProvider)
    {
        _eventRepository = eventRepository;
        _routeSequenceProvider = routeSequenceProvider;
    }

    [HttpGet("status")]
    public Task<IReadOnlyList<LineStatusSummary>> GetStatus(
        CancellationToken cancellationToken = default) =>
        _eventRepository.GetCurrentLineStatusAsync(cancellationToken);

    [HttpGet("{lineId}/route-sequences")]
    public Task<TflAnalytics.Contracts.Tfl.RouteSequence> GetRouteSequence(
        string lineId,
        CancellationToken cancellationToken = default) =>
        _routeSequenceProvider.GetAsync(lineId, cancellationToken);
}
