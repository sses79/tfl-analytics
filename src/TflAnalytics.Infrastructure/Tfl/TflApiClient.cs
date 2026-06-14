using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Infrastructure.Tfl;

public sealed class TflApiClient : ITflApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly TflApiOptions _options;

    public TflApiClient(HttpClient httpClient, IOptions<TflApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<IReadOnlyList<ArrivalPrediction>> GetArrivalsAsync(
        string stationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStationId = RequireId(stationId, nameof(stationId));
        return GetAsync<IReadOnlyList<ArrivalPrediction>>(
            $"StopPoint/{Uri.EscapeDataString(normalizedStationId)}/Arrivals",
            cancellationToken);
    }

    public Task<StopPoint> GetStopPointAsync(
        string stationId,
        CancellationToken cancellationToken = default)
    {
        var normalizedStationId = RequireId(stationId, nameof(stationId));
        return GetAsync<StopPoint>(
            $"StopPoint/{Uri.EscapeDataString(normalizedStationId)}",
            cancellationToken);
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

        return await GetAsync<IReadOnlyList<Line>>($"Line/{ids}/Status", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_options.AppKey))
        {
            path += $"?app_key={Uri.EscapeDataString(_options.AppKey)}";
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(path, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<T>(
                            SerializerOptions,
                            cancellationToken)
                        ?? throw new InvalidOperationException("TfL API returned an empty response.");
                }

                if (attempt < 2 && IsTransient(response.StatusCode))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
                    continue;
                }

                throw new HttpRequestException(
                    $"TfL API returned HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
            }
            catch (HttpRequestException exception)
                when (attempt < 2
                    && (exception.StatusCode is null || IsTransient(exception.StatusCode.Value)))
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
            }
            catch (HttpRequestException exception)
            {
                throw new HttpRequestException(
                    "TfL API request failed after bounded retries.",
                    null,
                    exception.StatusCode);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout
        || statusCode == HttpStatusCode.TooManyRequests
        || (int)statusCode >= 500;

    private static string RequireId(string value, string parameterName)
    {
        var normalized = value.Trim();
        return normalized.Length > 0
            ? normalized
            : throw new ArgumentException("A TfL identifier is required.", parameterName);
    }
}
