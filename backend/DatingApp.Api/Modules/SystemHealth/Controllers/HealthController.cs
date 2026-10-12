using DatingApp.Api.Modules.SystemHealth.DTOs;
using DatingApp.Api.Modules.SystemHealth.Services;
using Microsoft.AspNetCore.Mvc;

namespace DatingApp.Api.Modules.SystemHealth.Controllers;

[ApiController]
[Route("api/v1/health")]
public sealed class HealthController(IDatabaseProbe databaseProbe) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("ok", "DatingApp API đang hoạt động.", DateTimeOffset.UtcNow));

    [HttpGet("database")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthResponse>> Database(CancellationToken cancellationToken)
    {
        var result = await databaseProbe.CheckAsync(cancellationToken);
        return StatusCode(result.Ready ? 200 : 503,
            new HealthResponse(result.Status, result.Message, DateTimeOffset.UtcNow));
    }
}
