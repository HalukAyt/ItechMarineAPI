// Controllers/TelemetryController.cs
using System.Security.Claims;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Authorize(Roles = "BoatOwner")]
[Route("telemetry")]
public class TelemetryController : ControllerBase
{
    private readonly ITelemetryService _svc;
    public TelemetryController(ITelemetryService svc) => _svc = svc;
    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // POST /telemetry/query
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<TelemetryItemDto>>> Query([FromBody] TelemetryQueryDto q)
        => Ok(await _svc.QueryAsync(OwnerId, q));
}
