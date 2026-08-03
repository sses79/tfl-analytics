namespace TflAnalytics.Contracts.Tfl;

public sealed record JourneyPlan(IReadOnlyList<Journey> Journeys);

public sealed record Journey(
    int Duration,
    DateTimeOffset? StartDateTime,
    DateTimeOffset? ArrivalDateTime,
    IReadOnlyList<JourneyLeg> Legs);

public sealed record JourneyLeg(
    int Duration,
    JourneyPoint? DeparturePoint,
    JourneyPoint? ArrivalPoint,
    JourneyInstruction? Instruction,
    JourneyMode? Mode,
    IReadOnlyList<JourneyRouteOption>? RouteOptions,
    IReadOnlyList<JourneyDisruption>? Disruptions);

public sealed record JourneyPoint(string? NaptanId, string? CommonName);
public sealed record JourneyInstruction(string? Summary, string? Detailed);
public sealed record JourneyMode(string? Id, string? Name);
public sealed record JourneyRouteOption(string? Name, IReadOnlyList<string>? Directions);
public sealed record JourneyDisruption(string? Category, string? Description);
