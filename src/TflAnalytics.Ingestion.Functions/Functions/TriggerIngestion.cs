using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class TriggerIngestion
{
    private readonly IIngestionPoller _poller;
    private readonly ILogger<TriggerIngestion> _logger;

    public TriggerIngestion(
        IIngestionPoller poller,
        ILogger<TriggerIngestion> logger)
    {
        _poller = poller;
        _logger = logger;
    }

    [Function(nameof(TriggerIngestion))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "pull")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var arrivalsPublished = await _poller.PollArrivalsAsync(cancellationToken);
        var lineStatusPublished = await _poller.PollLineStatusAsync(cancellationToken);

        _logger.LogInformation(
            "Manual pull published {ArrivalCount} arrival and {LineStatusCount} line-status events.",
            arrivalsPublished,
            lineStatusPublished);

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            arrivalsPublished,
            lineStatusPublished
        });

        return response;
    }
}
