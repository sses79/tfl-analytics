using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Ingestion;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class StationsController : ControllerBase
{
    private readonly IngestionOptions _ingestionOptions;

    public StationsController(IngestionOptions ingestionOptions) =>
        _ingestionOptions = ingestionOptions;

    [HttpGet]
    public IReadOnlyList<StationSummary> GetAll() =>
        _ingestionOptions.StationIds
            .Select(id => new StationSummary(id, null))
            .ToList();
}
