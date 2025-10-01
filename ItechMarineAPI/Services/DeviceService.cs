using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Security;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _db;
    private readonly IProtectionService _ps;

    public DeviceService(AppDbContext db, IProtectionService ps)
    { _db = db; _ps = ps; }

    public async Task<(DeviceDto Device, RotateKeyResponseDto? Key)> CreateAsync(Guid ownerId, DeviceCreateDto dto)
    {
        var boat = await _db.Boats.FirstAsync(b => b.OwnerId == ownerId);

        var plain = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        var device = new Device
        {
            BoatId = boat.Id,
            Name = dto.Name,
            DeviceKeyHash = RefreshToken.Hash(plain),
            DeviceKeyProtected = _ps.ProtectDeviceKey(plain)
        };
        _db.Devices.Add(device);
        await _db.SaveChangesAsync();

        return (new DeviceDto(device.Id, device.Name, device.IsActive),
                new RotateKeyResponseDto(plain));
    }

    public async Task<IEnumerable<DeviceDto>> ListAsync(Guid ownerId)
    {
        var boatId = await _db.Boats.Where(b => b.OwnerId == ownerId).Select(b => b.Id).FirstAsync();
        return await _db.Devices.Where(d => d.BoatId == boatId)
            .Select(d => new DeviceDto(d.Id, d.Name, d.IsActive))
            .ToListAsync();
    }

    public async Task<RotateKeyResponseDto> RotateKeyAsync(Guid ownerId, Guid deviceId)
    {
        var device = await _db.Devices.Include(d => d.Boat).FirstAsync(d => d.Id == deviceId);
        if (device.Boat.OwnerId != ownerId) throw new UnauthorizedAccessException();

        var plain = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        device.DeviceKeyHash = RefreshToken.Hash(plain);
        device.DeviceKeyProtected = _ps.ProtectDeviceKey(plain);
        await _db.SaveChangesAsync();
        return new RotateKeyResponseDto(plain);
    }
}
