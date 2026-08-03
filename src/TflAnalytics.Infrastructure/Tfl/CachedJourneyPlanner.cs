using System.Collections.Concurrent;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Infrastructure.Tfl;

public sealed class CachedJourneyPlanner : IJourneyPlanner
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SearchCacheDuration = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, SearchCacheEntry> _searchCache = new();
    private readonly ITflApiClient _client;
    private readonly TimeProvider _timeProvider;

    public CachedJourneyPlanner(ITflApiClient client, TimeProvider timeProvider)
    {
        _client = client;
        _timeProvider = timeProvider;
    }

    public async Task<JourneyPlan> GetAsync(
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
        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Plan;
        }

        var plan = await _client.GetJourneyPlanAsync(
            fromStationId,
            toStationId,
            journeyPreference,
            accessibilityPreferences,
            cancellationToken);
        _cache[key] = new(plan, now.Add(CacheDuration));
        return plan;
    }

    public async Task<StopPointSearchResult> SearchStationsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var key = query.Trim().ToUpperInvariant();
        var now = _timeProvider.GetUtcNow();
        if (_searchCache.TryGetValue(key, out var cached) && cached.ExpiresAtUtc > now)
        {
            return cached.Result;
        }

        var result = await _client.SearchStopPointsAsync(query, cancellationToken);
        _searchCache[key] = new(result, now.Add(SearchCacheDuration));
        return result;
    }

    private sealed record CacheEntry(JourneyPlan Plan, DateTimeOffset ExpiresAtUtc);
    private sealed record SearchCacheEntry(StopPointSearchResult Result, DateTimeOffset ExpiresAtUtc);
}
