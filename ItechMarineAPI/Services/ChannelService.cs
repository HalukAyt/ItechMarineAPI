using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Mqtt;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

public class ChannelService : IChannelService
{
    private readonly AppDbContext _db;
    private readonly ICommandService _cmd;
    private readonly IMqttPublisher _mqtt;
    public ChannelService(AppDbContext db, ICommandService cmd, IMqttPublisher mqtt)
    { _db = db; _cmd = cmd; _mqtt = mqtt; }

    public async Task<ChannelDto> CreateAsync(Guid ownerId, ChannelCreateDto dto)
    {
        var boat = await _db.Boats.FirstAsync(b => b.OwnerId == ownerId);
        var ch = new Channel { BoatId = boat.Id, Name = dto.Name, Type = dto.Type, Pin = dto.Pin, State = false };
        _db.Channels.Add(ch); await _db.SaveChangesAsync();
        return new ChannelDto(ch.Id, ch.Name, ch.Type, ch.Pin, ch.State);
    }

    public async Task<IEnumerable<ChannelDto>> ListAsync(Guid ownerId)
    {
        var boatId = await _db.Boats.Where(b => b.OwnerId == ownerId).Select(b => b.Id).FirstAsync();
        return await _db.Channels.Where(c => c.BoatId == boatId)
          .Select(c => new ChannelDto(c.Id, c.Name, c.Type, c.Pin, c.State))
          .ToListAsync();
    }

    public async Task<ChannelDto> ToggleAsync(Guid ownerId, Guid channelId, ToggleChannelDto dto)
    {
        var ch = await _db.Channels
            .Include(c => c.Boat)
            .FirstOrDefaultAsync(c => c.Id == channelId && c.Boat.OwnerId == ownerId);

        if (ch == null)
            throw new KeyNotFoundException("Channel not found or not owned by user.");

        ch.State = dto.State ?? !ch.State;
        await _db.SaveChangesAsync();

        await _cmd.EnqueueToBoatAsync(ch.BoatId, "channel.set",
            new { channelId = ch.Id, pin = ch.Pin, state = ch.State });

        return new ChannelDto(ch.Id, ch.Name, ch.Type, ch.Pin, ch.State);
    }
}
