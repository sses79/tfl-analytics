using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace TflAnalytics.Processing.Functions.Functions;

public sealed class ProcessingHealth
{
    [Function(nameof(ProcessingHealth))]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")]
        HttpRequestData request)
    {
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            service = "tfl-analytics-processing",
            status = "healthy"
        });

        return response;
    }
}
