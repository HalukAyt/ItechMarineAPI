using System.Security.Claims;
using System.Text.Json;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Security;           // IProtectionService, HmacHelper burada
using ItechMarineAPI.Services.Interfaces;
using MarineControl.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Authorize(Roles = "BoatOwner")]
[Route("devices")]
public class DevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IProtectionService _ps;
    private readonly ICommandService _cmd;
    private readonly IDeviceService _devices;

    public DevicesController(
        IDeviceService devices,
        AppDbContext db,
        IProtectionService ps,
        ICommandService cmd)
    {
        _devices = devices;
        _db = db;
        _ps = ps;
        _cmd = cmd;
    }

    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Owner (BoatOwner) -> yeni cihaz oluştur (plain key header'da döner)
    [HttpPost]
    public async Task<ActionResult<DeviceDto>> Create([FromBody] DeviceCreateDto dto)
    {
        var (dev, key) = await _devices.CreateAsync(OwnerId, dto);
        if (key is not null && !string.IsNullOrWhiteSpace(key.NewDeviceKey))
            Response.Headers.Append("X-New-Device-Key", key.NewDeviceKey);
        return Ok(dev);
    }

    // Owner -> cihaz listesi
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DeviceDto>>> List()
        => Ok(await _devices.ListAsync(OwnerId));

    // Owner -> cihaz anahtar döndürme
    [HttpPost("{deviceId:guid}/rotate-key")]
    public async Task<ActionResult<RotateKeyResponseDto>> RotateKey(Guid deviceId)
        => Ok(await _devices.RotateKeyAsync(OwnerId, deviceId));

    // --- Cihaz uçları (AllowAnonymous + HMAC) ---

    // Cihaz -> komut çekme (poll)
    [HttpPost("{deviceId:guid}/pull-commands")]
    [AllowAnonymous]
    [EnableRateLimiting("device-rl")]
    public async Task<ActionResult<IEnumerable<CommandEnvelopeDto>>> PullCommands(Guid deviceId)
    {
        if (!Request.Headers.TryGetValue("X-Signature", out var sig))
            return Unauthorized();

        // İsteğin gövdesi boş olabilir; yine de imzalama için okunur
        var body = await new StreamReader(Request.Body).ReadToEndAsync();

        var device = await _db.Devices
            .Include(d => d.Boat)
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device is null || !device.IsActive)
            return NotFound();

        var plain = _ps.UnprotectDeviceKey(device.DeviceKeyProtected);
        if (!HmacHelper.Verify(body ?? "", sig!, plain))
            return Unauthorized();

        var list = await _cmd.DequeueForDeviceAsync(deviceId, 20);
        return Ok(list);
    }

    // Cihaz -> komut onay (ack)
    [HttpPost("{deviceId:guid}/ack")]
    [AllowAnonymous]
    [EnableRateLimiting("device-rl")]
    public async Task<IActionResult> Ack(Guid deviceId, [FromBody] CommandAckDto dto)
    {
        if (!Request.Headers.TryGetValue("X-Signature", out var sig))
            return Unauthorized();

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId);
        if (device is null || !device.IsActive)
            return NotFound();

        // Body değil, id string'iyle doğrula:
        var bodyToSign = $"{{\"id\":{dto.Id}}}";

        var plain = _ps.UnprotectDeviceKey(device.DeviceKeyProtected);
        if (!HmacHelper.Verify(bodyToSign, sig!, plain))
            return Unauthorized();

        await _cmd.AckAsync(deviceId, dto.Id);
        return Ok();
    }

}
