namespace TflAnalytics.Contracts.Dashboard;

public sealed record PassengerJourneyPlan(
    IReadOnlyList<PassengerJourney> Journeys,
    int DuplicateCountRemoved);

public sealed record PassengerJourney(
    string Id,
    IReadOnlyList<string> Labels,
    int DurationMinutes,
    DateTimeOffset? DepartureUtc,
    DateTimeOffset? ArrivalUtc,
    int ChangeCount,
    int WalkingMinutes,
    string? AccessibilitySummary,
    IReadOnlyList<PassengerJourneyLeg> Legs,
    IReadOnlyList<PassengerJourneyDisruption> Disruptions);

public sealed record PassengerJourneyLeg(
    string Kind,
    string Mode,
    string? LineName,
    string? Towards,
    string? FromStationId,
    string? FromName,
    string? ToStationId,
    string? ToName,
    int DurationMinutes,
    string Instruction);

public sealed record PassengerJourneyDisruption(
    string? Category,
    string Summary,
    IReadOnlyList<int> AffectedLegIndexes);

public sealed record StationSearchResponse(IReadOnlyList<PassengerStationMatch> Matches);

public sealed record PassengerStationMatch(
    string StationId,
    string DisplayName,
    IReadOnlyList<string> Modes,
    IReadOnlyList<string> Lines,
    bool IsDirect);
