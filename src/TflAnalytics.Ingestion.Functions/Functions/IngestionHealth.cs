using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class IngestionHealth
{
    [Function(nameof(IngestionHealth))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            service = "tfl-analytics-ingestion",
            status = "healthy"
        });

        return response;
    }
}
