using System.Collections.Concurrent;
using TflAnalytics.Application.Passenger;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Infrastructure.Tfl;

public sealed class CachedRouteSequenceProvider : IRouteSequenceProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, CacheEntry> _cache =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ITflApiClient _client;
    private readonly TimeProvider _timeProvider;

    public CachedRouteSequenceProvider(ITflApiClient client, TimeProvider timeProvider)
    {
        _client = client;
        _timeProvider = timeProvider;
    }

    public async Task<RouteSequence> GetAsync(
        string lineId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(lineId, out var cached)
            && cached.ExpiresAtUtc > _timeProvider.GetUtcNow())
        {
            return cached.Route;
        }

        var route = await _client.GetRouteSequenceAsync(lineId, cancellationToken);
        _cache[lineId] = new CacheEntry(
            route,
            _timeProvider.GetUtcNow().Add(CacheDuration));
        return route;
    }

    private sealed record CacheEntry(RouteSequence Route, DateTimeOffset ExpiresAtUtc);
}
