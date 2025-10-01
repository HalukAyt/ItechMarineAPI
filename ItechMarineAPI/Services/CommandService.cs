using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Services.Interfaces;   
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ItechMarineAPI.Services;

public class CommandService : ICommandService
{
    private readonly AppDbContext _db;
    public CommandService(AppDbContext db) => _db = db;

    public async Task EnqueueToBoatAsync(Guid boatId, string type, object payload)
    {
        var deviceIds = await _db.Devices.Where(d => d.BoatId == boatId && d.IsActive)
                                         .Select(d => d.Id).ToListAsync();
        if (!deviceIds.Any()) return;
        var json = JsonSerializer.Serialize(payload);
        foreach (var did in deviceIds)
            _db.DeviceCommands.Add(new DeviceCommand { DeviceId = did, Type = type, PayloadJson = json });
        await _db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CommandEnvelopeDto>> DequeueForDeviceAsync(Guid deviceId, int max = 10)
    {
        var list = await _db.DeviceCommands
            .Where(c => c.DeviceId == deviceId && c.DequeuedAt == null && c.AckedAt == null)
            .OrderBy(c => c.Id).Take(max).ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var c in list) c.DequeuedAt = now;
        await _db.SaveChangesAsync();

        return list.Select(c => new CommandEnvelopeDto(c.Id, c.Type, c.PayloadJson, c.CreatedAt)).ToList();
    }

    public async Task AckAsync(Guid deviceId, long commandId)
    {
        var cmd = await _db.DeviceCommands.FirstOrDefaultAsync(c => c.Id == commandId && c.DeviceId == deviceId);
        if (cmd != null) { cmd.AckedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
    }
}
