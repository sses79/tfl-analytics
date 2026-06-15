using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Infrastructure.Realtime;

public sealed class LocalRelayRealtimeNotifier : IRealtimeNotifier
{
    private readonly Uri _relayBaseUri;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalRelayRealtimeNotifier> _logger;

    public LocalRelayRealtimeNotifier(
        string relayBaseUrl,
        HttpClient httpClient,
        ILogger<LocalRelayRealtimeNotifier> logger)
    {
        _relayBaseUri = new Uri($"{relayBaseUrl.TrimEnd('/')}/");
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task BroadcastArrivalsAsync(
        ArrivalsUpdated message,
        CancellationToken cancellationToken = default) =>
        SendAsync("arrivals", message, cancellationToken);

    public Task BroadcastLineStatusAsync(
        LineStatusChanged message,
        CancellationToken cancellationToken = default) =>
        SendAsync("line-status", message, cancellationToken);

    public Task BroadcastAlertAsync(
        AlertRaised message,
        CancellationToken cancellationToken = default) =>
        SendAsync("alerts", message, cancellationToken);

    private async Task SendAsync<T>(
        string route,
        T message,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                new Uri(_relayBaseUri, route),
                message,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Local SignalR relay failed: {StatusCode} {Route}.",
                    (int)response.StatusCode,
                    route);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Local SignalR relay error for route {Route}.", route);
        }
    }
}
