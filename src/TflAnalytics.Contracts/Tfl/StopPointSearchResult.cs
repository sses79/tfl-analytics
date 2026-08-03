namespace TflAnalytics.Contracts.Tfl;

public sealed record StopPointSearchResult(IReadOnlyList<StopPointSearchMatch> Matches);
public sealed record StopPointSearchMatch(string Id, string Name, IReadOnlyList<string>? Modes);
