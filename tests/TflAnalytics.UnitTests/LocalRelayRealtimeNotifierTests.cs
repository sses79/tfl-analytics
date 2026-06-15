using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TflAnalytics.Contracts.Realtime;
using TflAnalytics.Infrastructure.Realtime;

namespace TflAnalytics.UnitTests;

public sealed class LocalRelayRealtimeNotifierTests
{
    [Fact]
    public async Task PostsEachMessageTypeToItsRelayRoute()
    {
        var handler = new RecordingHttpMessageHandler();
        var notifier = new LocalRelayRealtimeNotifier(
            "http://api:8080/internal/realtime",
            new HttpClient(handler),
            NullLogger<LocalRelayRealtimeNotifier>.Instance);
        var observedAt = DateTimeOffset.Parse("2026-06-15T12:00:00Z");

        await notifier.BroadcastArrivalsAsync(
            new ArrivalsUpdated(
                "940GZZLUVIC",
                "Victoria Underground Station",
                "victoria",
                "Victoria",
                "Walthamstow Central",
                "Northbound",
                "inbound",
                observedAt.AddSeconds(45),
                45,
                observedAt));
        await notifier.BroadcastLineStatusAsync(
            new LineStatusChanged(
                "victoria",
                "Victoria",
                9,
                "Minor Delays",
                "Fixture disruption",
                observedAt));
        await notifier.BroadcastAlertAsync(
            new AlertRaised(
                "alert-1",
                "LineStatusDisruption",
                null,
                "victoria",
                "Victoria disruption",
                "Minor delays detected",
                "Good Service",
                "Minor Delays",
                observedAt));

        Assert.Collection(
            handler.Requests,
            request =>
            {
                Assert.Equal(
                    "http://api:8080/internal/realtime/arrivals",
                    request.Uri.ToString());
                Assert.Equal("940GZZLUVIC", request.Body.GetProperty("stationId").GetString());
            },
            request =>
            {
                Assert.Equal(
                    "http://api:8080/internal/realtime/line-status",
                    request.Uri.ToString());
                Assert.Equal("victoria", request.Body.GetProperty("lineId").GetString());
            },
            request =>
            {
                Assert.Equal(
                    "http://api:8080/internal/realtime/alerts",
                    request.Uri.ToString());
                Assert.Equal("alert-1", request.Body.GetProperty("alertId").GetString());
            });
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await JsonDocument.ParseAsync(
                await request.Content!.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            Requests.Add(new RecordedRequest(request.RequestUri!, body.RootElement.Clone()));
            body.Dispose();

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        }
    }

    private sealed record RecordedRequest(Uri Uri, JsonElement Body);
}
