using Microsoft.AspNetCore.Mvc;
using TflAnalytics.Application.Tfl;
using TflAnalytics.Contracts.Tfl;

namespace TflAnalytics.Api.Controllers;

[ApiController]
[Route("api/tfl")]
public sealed class TflController : ControllerBase
{
    private readonly ITflApiClient _tflApiClient;

    public TflController(ITflApiClient tflApiClient)
    {
        _tflApiClient = tflApiClient;
    }

    [HttpGet("line-status/{ids}")]
    [ProducesResponseType<IReadOnlyList<Line>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<Line>>> GetLineStatus(
        string ids,
        CancellationToken cancellationToken)
    {
        var result = await _tflApiClient.GetLineStatusAsync(
            ids.Split(',', StringSplitOptions.RemoveEmptyEntries),
            cancellationToken);

        return Ok(result);
    }
}
