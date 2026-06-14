using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TflAnalytics.Infrastructure.Tfl;

namespace TflAnalytics.UnitTests;

public sealed class TflApiClientTests
{
    [Fact]
    public async Task CallsArrivalAndStopPointOperations()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            var json = request.RequestUri!.AbsolutePath.EndsWith("/Arrivals")
                ? """
                  [{
                    "id": "prediction-1",
                    "vehicleId": "245",
                    "naptanId": "940GZZLUVIC",
                    "stationName": "Victoria Underground Station",
                    "lineId": "victoria",
                    "lineName": "Victoria",
                    "destinationName": "Walthamstow Central Underground Station",
                    "platformName": "Northbound - Platform 3",
                    "direction": "inbound",
                    "expectedArrival": "2026-06-13T12:00:45Z",
                    "timeToStation": 45,
                    "timestamp": "2026-06-13T12:00:00Z"
                  }]
                  """
                : """
                  {
                    "naptanId": "940GZZLUVIC",
                    "commonName": "Victoria Underground Station",
                    "stopType": "NaptanMetroStation",
                    "lines": [{ "id": "victoria", "name": "Victoria" }]
                  }
                  """;

            return JsonResponse(HttpStatusCode.OK, json);
        });
        var client = CreateClient(handler);

        var arrivals = await client.GetArrivalsAsync("940GZZLUVIC");
        var stopPoint = await client.GetStopPointAsync("940GZZLUVIC");

        Assert.Equal("245", Assert.Single(arrivals).VehicleId);
        Assert.Equal("Victoria Underground Station", stopPoint.CommonName);
        Assert.Equal(
            [
                "/StopPoint/940GZZLUVIC/Arrivals",
                "/StopPoint/940GZZLUVIC"
            ],
            handler.Paths);
    }

    [Fact]
    public async Task RetriesTransientResponsesWithABound()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler(_ =>
        {
            attempts++;
            return attempts < 3
                ? JsonResponse(HttpStatusCode.ServiceUnavailable, "{}")
                : JsonResponse(HttpStatusCode.OK, "[]");
        });
        var client = CreateClient(handler);

        var lines = await client.GetLineStatusAsync(["victoria"]);

        Assert.Empty(lines);
        Assert.Equal(3, attempts);
    }

    private static TflApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.tfl.test/")
        };

        return new TflApiClient(
            httpClient,
            Options.Create(new TflApiOptions
            {
                BaseUrl = httpClient.BaseAddress.ToString()
            }));
    }

    private static HttpResponseMessage JsonResponse(
        HttpStatusCode statusCode,
        string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(_responseFactory(request));
        }
    }
}
