using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Processing;
using TflAnalytics.Contracts.Dashboard;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/stations")]
public sealed class ArrivalsController : ControllerBase
{
    private readonly IEventRepository _eventRepository;
    private readonly IDepartureBoardService _departureBoardService;

    public ArrivalsController(
        IEventRepository eventRepository,
        IDepartureBoardService departureBoardService)
    {
        _eventRepository = eventRepository;
        _departureBoardService = departureBoardService;
    }

    [HttpGet("{stationId}/arrivals")]
    public Task<IReadOnlyList<ArrivalSummary>> GetArrivals(
        string stationId,
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default) =>
        _eventRepository.GetRecentArrivalsAsync(stationId, count, cancellationToken);

    [HttpGet("{stationId}/departure-board")]
    public Task<DepartureBoard> GetDepartureBoard(
        string stationId,
        [FromQuery] string? destinationStationId = null,
        CancellationToken cancellationToken = default) =>
        _departureBoardService.GetAsync(
            stationId,
            destinationStationId,
            cancellationToken);

    [HttpGet("{stationId}/destinations")]
    public async Task<IReadOnlyList<DestinationOption>> GetDestinations(
        string stationId,
        CancellationToken cancellationToken = default) =>
        (await _departureBoardService.GetAsync(
            stationId,
            cancellationToken: cancellationToken)).Destinations;
}
