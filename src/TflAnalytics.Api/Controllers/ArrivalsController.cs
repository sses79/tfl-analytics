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
    private readonly IJourneyPlanner _journeyPlanner;

    public ArrivalsController(
        IEventRepository eventRepository,
        IDepartureBoardService departureBoardService,
        IJourneyPlanner journeyPlanner)
    {
        _eventRepository = eventRepository;
        _departureBoardService = departureBoardService;
        _journeyPlanner = journeyPlanner;
    }

    [HttpGet("{stationId}/arrivals")]
    public Task<IReadOnlyList<ArrivalSummary>> GetArrivals(
        string stationId,
        [FromQuery] int count = 20,
        CancellationToken cancellationToken = default) =>
        _eventRepository.GetRecentArrivalsAsync(stationId, count, cancellationToken);

    [HttpGet("search")]
    public Task<StationSearchResponse> SearchStations(
        [FromQuery] string query,
        [FromQuery] string? originStationId = null,
        CancellationToken cancellationToken = default) =>
        _journeyPlanner.SearchStationsAsync(query, originStationId, cancellationToken);

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

    [HttpGet("{stationId}/journeys/{destinationStationId}")]
    public Task<PassengerJourneyPlan> GetJourneys(
        string stationId,
        string destinationStationId,
        [FromQuery] string preference = "leastinterchange",
        [FromQuery] string[]? accessibility = null,
        CancellationToken cancellationToken = default) =>
        _journeyPlanner.GetAsync(
            stationId,
            destinationStationId,
            preference,
            accessibility ?? [],
            cancellationToken);
}
