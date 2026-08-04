using System.Collections.Concurrent;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Dashboard;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Infrastructure.Tfl;

public sealed class CachedJourneyPlanner : IJourneyPlanner
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromHours(24);
    private const int MaximumSearchEntries = 100;
    private readonly ConcurrentDictionary<string, JourneyCacheEntry> _journeyCache = new();
    private readonly ConcurrentDictionary<string, SearchCacheEntry> _searchCache = new();
    private readonly ITflApiClient _client;
    private readonly IDepartureBoardService _departureBoards;
    private readonly TimeProvider _timeProvider;

    public CachedJourneyPlanner(
        ITflApiClient client,
        IDepartureBoardService departureBoards,
        TimeProvider timeProvider)
    {
        _client = client;
        _departureBoards = departureBoards;
        _timeProvider = timeProvider;
    }

    public async Task<PassengerJourneyPlan> GetAsync(
        string fromStationId,
        string toStationId,
        string journeyPreference,
        IReadOnlyList<string> accessibilityPreferences,
        CancellationToken cancellationToken = default)
    {
        var key = string.Join('|',
            fromStationId.Trim().ToUpperInvariant(),
            toStationId.Trim().ToUpperInvariant(),
            journeyPreference.Trim().ToLowerInvariant(),
            string.Join(',', accessibilityPreferences.Order(StringComparer.OrdinalIgnoreCase)));
        var now = _timeProvider.GetUtcNow();
        if (_journeyCache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Plan;
        }

        var raw = await _client.GetJourneyPlanAsync(
            fromStationId,
            toStationId,
            journeyPreference,
            accessibilityPreferences,
            cancellationToken);
        var plan = PassengerJourneyNormalizer.NormalizeJourneys(raw, journeyPreference, accessibilityPreferences.Count > 0);
        _journeyCache[key] = new(plan, now.Add(CacheDuration));
        return plan;
    }

    public async Task<StationSearchResponse> SearchStationsAsync(
        string query,
        string? originStationId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = PassengerJourneyNormalizer.NormalizeStationName(query);
        if (normalizedQuery.Length < 2)
        {
            return new StationSearchResponse([]);
        }

        var key = $"{originStationId?.Trim().ToUpperInvariant()}|{normalizedQuery.ToUpperInvariant()}";
        var now = _timeProvider.GetUtcNow();
        if (_searchCache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Result;
        }

        var raw = await _client.SearchStopPointsAsync(query, cancellationToken);
        var directDestinations = string.IsNullOrWhiteSpace(originStationId)
            ? []
            : await _departureBoards.GetDestinationsAsync(originStationId, cancellationToken);
        var result = PassengerJourneyNormalizer.NormalizeStations(raw, directDestinations, normalizedQuery);

        if (_searchCache.Count >= MaximumSearchEntries)
        {
            foreach (var expired in _searchCache.Where(entry => entry.Value.ExpiresAtUtc <= now))
            {
                _searchCache.TryRemove(expired.Key, out _);
            }

            if (_searchCache.Count >= MaximumSearchEntries)
            {
                var oldest = _searchCache.MinBy(entry => entry.Value.ExpiresAtUtc);
                if (!string.IsNullOrEmpty(oldest.Key)) _searchCache.TryRemove(oldest.Key, out _);
            }
        }
        _searchCache[key] = new(result, now.Add(SearchCacheDuration));
        return result;
    }

    private sealed record JourneyCacheEntry(PassengerJourneyPlan Plan, DateTimeOffset ExpiresAtUtc);
    private sealed record SearchCacheEntry(StationSearchResponse Result, DateTimeOffset ExpiresAtUtc);
}
