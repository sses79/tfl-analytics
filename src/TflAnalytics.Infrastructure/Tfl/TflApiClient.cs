using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Infrastructure.Tfl;

public sealed class TflApiClient : ITflApiClient
{
    private readonly HttpClient _httpClient;
    private readonly TflApiOptions _options;

    public TflApiClient(HttpClient httpClient, IOptions<TflApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<Line>> GetLineStatusAsync(
        IEnumerable<string> lineIds,
        CancellationToken cancellationToken = default)
    {
        var ids = string.Join(
            ",",
            lineIds
                .Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .Select(Uri.EscapeDataString));

        if (ids.Length == 0)
        {
            throw new ArgumentException("At least one TfL line ID is required.", nameof(lineIds));
        }

        var path = $"Line/{ids}/Status";
        if (!string.IsNullOrWhiteSpace(_options.AppKey))
        {
            path += $"?app_key={Uri.EscapeDataString(_options.AppKey)}";
        }

        return await _httpClient.GetFromJsonAsync<List<Line>>(path, cancellationToken)
            ?? [];
    }
}
