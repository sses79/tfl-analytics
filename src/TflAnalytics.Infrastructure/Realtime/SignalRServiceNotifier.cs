using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Realtime;
using TflAnalytics.Contracts.Realtime;

namespace TflAnalytics.Infrastructure.Realtime;

public sealed class SignalRServiceNotifier : IRealtimeNotifier
{
    private const string HubName = "DashboardHub";
    private static readonly string[] SignalRScopes = ["https://signalr.azure.com/.default"];
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly string _broadcastUrl;
    private readonly TokenCredential _credential;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SignalRServiceNotifier> _logger;

    public SignalRServiceNotifier(
        string endpoint,
        ILogger<SignalRServiceNotifier> logger)
    {
        _broadcastUrl = $"{endpoint.TrimEnd('/')}/api/v1/hubs/{HubName}";
        _credential = new DefaultAzureCredential();
        _httpClient = new HttpClient();
        _logger = logger;
    }

    public Task BroadcastArrivalsAsync(ArrivalsUpdated message, CancellationToken cancellationToken = default) =>
        SendAsync("arrivalsUpdated", message, cancellationToken);

    public Task BroadcastLineStatusAsync(LineStatusChanged message, CancellationToken cancellationToken = default) =>
        SendAsync("lineStatusChanged", message, cancellationToken);

    public Task BroadcastAlertAsync(AlertRaised message, CancellationToken cancellationToken = default) =>
        SendAsync("alertRaised", message, cancellationToken);

    private async Task SendAsync<T>(string target, T payload, CancellationToken cancellationToken)
    {
        try
        {
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(SignalRScopes),
                cancellationToken);

            var body = JsonSerializer.Serialize(
                new { target, arguments = new object[] { payload! } },
                JsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, _broadcastUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SignalR broadcast failed: {StatusCode} {Target}.",
                    (int)response.StatusCode,
                    target);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "SignalR broadcast error for target {Target}.", target);
        }
    }
}
