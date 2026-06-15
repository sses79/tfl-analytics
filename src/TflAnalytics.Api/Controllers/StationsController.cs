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

    private static readonly Dictionary<string, string> StationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["940GZZLUVIC"] = "Victoria",
        ["940GZZLUOXC"] = "Oxford Circus",
        ["940GZZLUGPK"] = "Green Park",
        ["940GZZLUKSX"] = "King's Cross St. Pancras",
        ["940GZZLULNB"] = "London Bridge"
    };

    [HttpGet]
    public IReadOnlyList<StationSummary> GetAll() =>
        _ingestionOptions.StationIds
            .Select(id => new StationSummary(id, StationNames.GetValueOrDefault(id)))
            .ToList();
}
