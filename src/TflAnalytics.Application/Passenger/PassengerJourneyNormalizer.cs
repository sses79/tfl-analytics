using System.Security.Cryptography;
using System.Text;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Application.Passenger;

public static class PassengerJourneyNormalizer
{
    public static PassengerJourneyPlan NormalizeJourneys(
        JourneyPlan raw,
        string preference,
        bool accessibilityRequested)
    {
        var normalized = raw.Journeys.Select(journey => NormalizeJourney(journey, accessibilityRequested)).ToArray();
        var distinct = normalized
            .GroupBy(journey => journey.Signature, StringComparer.Ordinal)
            .SelectMany(group => group
                .OrderBy(journey => journey.DurationMinutes)
                .ThenBy(journey => journey.WalkingMinutes)
                .Aggregate(new List<NormalizedJourney>(), (kept, candidate) =>
                {
                    if (!kept.Any(existing => DepartureDifference(existing.DepartureUtc, candidate.DepartureUtc) < TimeSpan.FromMinutes(5)))
                    {
                        kept.Add(candidate);
                    }
                    return kept;
                }))
            .ToList();

        var ordered = OrderJourneys(distinct, preference).Take(3).ToList();
        if (ordered.Count > 0)
        {
            ordered[0].Labels.Add("Recommended");
            AddLabel(ordered, distinct.MinBy(item => item.DurationMinutes), "Fastest");
            AddLabel(ordered, distinct.MinBy(item => (item.ChangeCount, item.DurationMinutes)), "Fewer changes");
            AddLabel(ordered, distinct.MinBy(item => (item.WalkingMinutes, item.DurationMinutes)), "Less walking");
        }

        return new PassengerJourneyPlan(
            ordered.Select(ToContract).ToArray(),
            Math.Max(0, raw.Journeys.Count - distinct.Count));
    }

    public static string NormalizeStationName(string? name) => (name ?? string.Empty).Trim()
        .Replace(" Underground Station", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace(" Rail Station", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace(" Station", string.Empty, StringComparison.OrdinalIgnoreCase);

    public static StationSearchResponse NormalizeStations(
        StopPointSearchResult raw,
        IReadOnlyList<DestinationOption> directDestinations,
        string query)
    {
        var normalizedQuery = NormalizeStationName(query);
        var directById = directDestinations.ToDictionary(item => item.StationId, StringComparer.OrdinalIgnoreCase);
        var matches = raw.Matches
            .Where(match => IsRailStation(match.Modes))
            .Select(match => new PassengerStationMatch(
                match.Id,
                NormalizeStationName(match.Name),
                match.Modes?.Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [],
                directById.TryGetValue(match.Id, out var direct) ? direct.LineIds : [],
                directById.ContainsKey(match.Id)))
            .GroupBy(match => match.StationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(match => SearchRank(match.DisplayName, normalizedQuery))
            .ThenByDescending(match => match.IsDirect)
            .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(match => match.StationId, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();
        return new StationSearchResponse(matches);
    }

    private static NormalizedJourney NormalizeJourney(Journey journey, bool accessibilityRequested)
    {
        var legs = journey.Legs.Select((leg, index) => NormalizeLeg(leg, index, journey.Legs.Count)).ToArray();
        var transportLegs = legs.Where(leg => leg.Kind == "transport").ToArray();
        var signature = string.Join('>', transportLegs.Select(leg => string.Join(':',
            leg.Mode.ToLowerInvariant(), leg.LineName?.ToLowerInvariant(),
            NormalizePoint(leg.FromStationId, leg.FromName), NormalizePoint(leg.ToStationId, leg.ToName))));
        var disruptions = journey.Legs
            .SelectMany((leg, index) => (leg.Disruptions ?? []).Select(disruption => new { disruption, index }))
            .Where(item => !string.IsNullOrWhiteSpace(item.disruption.Description))
            .GroupBy(item => $"{item.disruption.Category}|{item.disruption.Description}", StringComparer.OrdinalIgnoreCase)
            .Select(group => new PassengerJourneyDisruption(
                group.First().disruption.Category,
                group.First().disruption.Description!.Trim(),
                group.Select(item => item.index).Distinct().Order().ToArray()))
            .ToArray();
        var accessConflict = accessibilityRequested && disruptions.Any(IsAccessibilityDisruption);
        return new(signature, journey.Duration, journey.StartDateTime, journey.ArrivalDateTime,
            Math.Max(0, transportLegs.Length - 1),
            legs.Where(leg => leg.Kind != "transport").Sum(leg => leg.DurationMinutes),
            accessConflict ? "Does not meet selected access need" : null, legs, disruptions, []);
    }

    private static PassengerJourneyLeg NormalizeLeg(JourneyLeg leg, int index, int legCount)
    {
        var mode = leg.Mode?.Name ?? leg.Mode?.Id ?? "travel";
        var walking = string.Equals(mode, "walking", StringComparison.OrdinalIgnoreCase);
        var kind = walking ? index == 0 ? "enter" : index == legCount - 1 ? "exit" : "walk" : "transport";
        var lineName = leg.RouteOptions?.Select(option => option.Name).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
        var towards = leg.RouteOptions?.SelectMany(option => option.Directions ?? []).FirstOrDefault();
        var instruction = kind switch
        {
            "enter" => $"Enter {NormalizeStationName(leg.ArrivalPoint?.CommonName ?? leg.DeparturePoint?.CommonName ?? "station")}",
            "exit" => $"Exit at {NormalizeStationName(leg.ArrivalPoint?.CommonName ?? "destination")}",
            _ => leg.Instruction?.Summary
                ?? $"{NormalizeStationName(leg.DeparturePoint?.CommonName ?? "Start")} to {NormalizeStationName(leg.ArrivalPoint?.CommonName ?? "destination")}"
        };
        return new PassengerJourneyLeg(kind, mode, lineName, towards,
            leg.DeparturePoint?.NaptanId, NormalizeStationName(leg.DeparturePoint?.CommonName),
            leg.ArrivalPoint?.NaptanId, NormalizeStationName(leg.ArrivalPoint?.CommonName),
            leg.Duration, instruction);
    }

    private static IEnumerable<NormalizedJourney> OrderJourneys(IEnumerable<NormalizedJourney> journeys, string preference) =>
        preference switch
        {
            "leasttime" => journeys.OrderBy(item => item.AccessibilitySummary is not null).ThenBy(item => item.DurationMinutes).ThenBy(item => item.ChangeCount),
            "leastwalking" => journeys.OrderBy(item => item.AccessibilitySummary is not null).ThenBy(item => item.WalkingMinutes).ThenBy(item => item.DurationMinutes),
            _ => journeys.OrderBy(item => item.AccessibilitySummary is not null).ThenBy(item => item.ChangeCount).ThenBy(item => item.DurationMinutes)
        };

    private static void AddLabel(List<NormalizedJourney> displayed, NormalizedJourney? target, string label)
    {
        var match = target is null ? null : displayed.FirstOrDefault(item => ReferenceEquals(item, target));
        if (match is not null && !match.Labels.Contains(label, StringComparer.Ordinal)) match.Labels.Add(label);
    }

    private static PassengerJourney ToContract(NormalizedJourney journey) => new(
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(journey.Signature)))[..12].ToLowerInvariant(),
        journey.Labels, journey.DurationMinutes, journey.DepartureUtc, journey.ArrivalUtc,
        journey.ChangeCount, journey.WalkingMinutes, journey.AccessibilitySummary, journey.Legs, journey.Disruptions);

    private static TimeSpan DepartureDifference(DateTimeOffset? left, DateTimeOffset? right) =>
        left is null || right is null ? TimeSpan.MaxValue : (left.Value - right.Value).Duration();
    private static bool IsAccessibilityDisruption(PassengerJourneyDisruption disruption)
    {
        if (disruption.Category?.Contains("access", StringComparison.OrdinalIgnoreCase) is true) return true;

        var summary = disruption.Summary;
        return summary.Contains("step free", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("step-free", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("lift", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("wheelchair", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("mobility", StringComparison.OrdinalIgnoreCase);
    }
    private static string NormalizePoint(string? id, string? name) =>
        !string.IsNullOrWhiteSpace(id) ? id.ToUpperInvariant() : NormalizeStationName(name).ToUpperInvariant();
    private static int SearchRank(string name, string query) =>
        string.Equals(name, query, StringComparison.OrdinalIgnoreCase) ? 0
        : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 1
        : name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    private static bool IsRailStation(IReadOnlyList<string>? modes) => modes is not null && modes.Any(mode =>
        string.Equals(mode, "tube", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "dlr", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "overground", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "elizabeth-line", StringComparison.OrdinalIgnoreCase));

    private sealed record NormalizedJourney(
        string Signature, int DurationMinutes, DateTimeOffset? DepartureUtc, DateTimeOffset? ArrivalUtc,
        int ChangeCount, int WalkingMinutes, string? AccessibilitySummary,
        IReadOnlyList<PassengerJourneyLeg> Legs,
        IReadOnlyList<PassengerJourneyDisruption> Disruptions,
        List<string> Labels);
}
