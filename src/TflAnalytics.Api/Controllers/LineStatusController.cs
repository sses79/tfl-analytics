using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/lines")]
public sealed class LineStatusController : ControllerBase
{
    private readonly IEventRepository _eventRepository;

    public LineStatusController(IEventRepository eventRepository) =>
        _eventRepository = eventRepository;

    [HttpGet("status")]
    public Task<IReadOnlyList<LineStatusSummary>> GetStatus(
        CancellationToken cancellationToken = default) =>
        _eventRepository.GetCurrentLineStatusAsync(cancellationToken);
}
