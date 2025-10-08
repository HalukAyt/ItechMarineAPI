using System.Text;
using System.Text.Json;
using ItechMarineAPI.Data;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Security;
using MarineControl.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Route("ingest")]
public class IngestController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProtectionService _ps;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public IngestController(AppDbContext db, IProtectionService ps)
    {
        _db = db; _ps = ps;
    }

    public sealed class TelemetryInDto
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
        public DateTime? TimestampUtc { get; set; }
    }

    [HttpPost("telemetry")]
    [AllowAnonymous]
    [EnableRateLimiting("device-rl")]
    public async Task<IActionResult> Telemetry()
    {
        // 1) Header’lar
        if (!Request.Headers.TryGetValue("X-Device-Id", out var devIdRaw)) return Unauthorized();
        if (!Guid.TryParse(devIdRaw, out var deviceId)) return Unauthorized();
        if (!Request.Headers.TryGetValue("X-Signature", out var sig)) return Unauthorized();

        // 2) Ham gövdeyi oku (tek sefer)
        Request.EnableBuffering();
        using var sr = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await sr.ReadToEndAsync();
        Request.Body.Position = 0; // (gerekirse tekrar okumak için)

        // 3) Cihaz + HMAC doğrulama
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.IsActive);
        if (device is null) return NotFound();

        var plain = _ps.UnprotectDeviceKey(device.DeviceKeyProtected);
        if (!HmacHelper.Verify(body, sig!, plain)) return Unauthorized();

        // 4) JSON → DTO (manuel)
        var dto = JsonSerializer.Deserialize<TelemetryInDto>(body, JsonOpts);
        if (dto is null || string.IsNullOrWhiteSpace(dto.Key) || dto.Value is null)
            return BadRequest("Invalid telemetry payload");

        // 5) Kaydet
        var t = new Telemetry
        {
            DeviceId = deviceId,
            Key = dto.Key,                    // NOT NULL
            Value = dto.Value,
            CreatedAt = dto.TimestampUtc ?? DateTime.UtcNow
        };
        _db.Telemetries.Add(t);
        await _db.SaveChangesAsync();

        return Ok();
    }
}
