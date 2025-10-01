using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Realtime;
using ItechMarineAPI.Security;
using MarineControl.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[ApiController]
[Route("ingest")]
public class IngestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProtectionService _ps;
    private readonly IHubContext<BoatHub> _hub;

    public IngestController(AppDbContext db, IProtectionService ps, IHubContext<BoatHub> hub)
    { _db = db; _ps = ps; _hub = hub; }

    [HttpPost("telemetry")]
    public async Task<IActionResult> Telemetry()
    {
        if (!Request.Headers.TryGetValue("X-Device-Id", out var deviceIdStr) ||
            !Request.Headers.TryGetValue("X-Signature", out var signature)) return Unauthorized();

        if (!Guid.TryParse(deviceIdStr!, out var deviceId)) return Unauthorized();

        var body = await new StreamReader(Request.Body).ReadToEndAsync();
        var device = await _db.Devices.Include(d => d.Boat).FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null || !device.IsActive) return NotFound();

        var plainKey = _ps.UnprotectDeviceKey(device.DeviceKeyProtected);
        if (!HmacHelper.Verify(body, signature!, plainKey)) return Unauthorized();

        var dto = JsonSerializer.Deserialize<TelemetryInDto>(body);
        if (dto is null) return BadRequest();

        var tel = new ItechMarineAPI.Entities.Telemetry
        {
            BoatId = device.BoatId,
            DeviceId = device.Id,
            Key = dto.Key,
            Value = dto.Value,
            CreatedAt = dto.TimestampUtc ?? DateTime.UtcNow
        };
        _db.Telemetries.Add(tel); await _db.SaveChangesAsync();

        // Canlı yayın
        await _hub.Clients.Group($"boat:{device.BoatId}")
            .SendAsync("telemetry", new { tel.Key, tel.Value, tel.CreatedAt });

        return Ok();
    }
}
