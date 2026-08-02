using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using TflAnalytics.Application.Ingestion;

namespace TflAnalytics.Ingestion.Functions.Functions;

public sealed class ArrivalDemoPolling
{
    private readonly IArrivalDemoPollingControl _control;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ArrivalDemoPolling> _logger;

    public ArrivalDemoPolling(
        IArrivalDemoPollingControl control,
        TimeProvider timeProvider,
        ILogger<ArrivalDemoPolling> logger)
    {
        _control = control;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    [Function(nameof(EnableArrivalDemoPolling))]
    public async Task<HttpResponseData> EnableArrivalDemoPolling(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "operations/arrival-demo-polling")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var nowUtc = _timeProvider.GetUtcNow();
        var expiry = await _control.EnableAsync(
            nowUtc,
            ArrivalDemoPollingControl.MaximumDuration,
            cancellationToken);
        _logger.LogWarning(
            "Operator activated the ten-minute arrival demo polling boost.");

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            enabled = true,
            pollingIntervalMinutes = 1,
            expiresAtUtc = expiry,
            defaultPollingIntervalMinutes = 5
        }, cancellationToken);
        return response;
    }

    [Function(nameof(GetArrivalDemoPollingStatus))]
    public async Task<HttpResponseData> GetArrivalDemoPollingStatus(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "operations/arrival-demo-polling")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var decision = await _control.EvaluateAsync(
            _timeProvider.GetUtcNow(),
            cancellationToken);
        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            enabled = decision.DemoBoostActive,
            pollingIntervalMinutes = decision.DemoBoostActive ? 1 : 5,
            expiresAtUtc = decision.DemoBoostExpiresAtUtc,
            reason = decision.Reason
        }, cancellationToken);
        return response;
    }
}
