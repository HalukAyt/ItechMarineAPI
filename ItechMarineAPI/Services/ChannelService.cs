using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItechMarineAPI.Services
{
    public class ChannelService : IChannelService
    {
        private readonly AppDbContext _db;
        private readonly ICommandService _cmd;
        private readonly ILogger<ChannelService> _logger;

        public ChannelService(AppDbContext db, ICommandService cmd, ILogger<ChannelService> logger)
        {
            _db = db;
            _cmd = cmd;
            _logger = logger;
        }

        /// <summary>
        /// Belirli bir owner’ın tüm kanallarını listeler.
        /// </summary>
        public async Task<IReadOnlyList<ChannelDto>> ListAsync(Guid ownerId)
        {
            var items = await _db.Channels
                .Include(c => c.Boat)
                .Where(c => c.Boat != null && c.Boat.OwnerId == ownerId)
                .OrderBy(c => c.Name)
                .ThenBy(c => c.Pin)
                .ToListAsync();

            return items.Select(c => new ChannelDto(c.Id, c.Name, c.Type, c.Pin, c.State)).ToList();
        }

        /// <summary>
        /// Kanal oluşturur (owner’a ait ilk tekne üzerinde). Başlangıç state=false.
        /// </summary>
        public async Task<ChannelDto> CreateAsync(Guid ownerId, ChannelCreateDto dto)
        {
            // Owner'ın ilk teknesini al (çoklu tekne desteği gerekirse DTO genişletilir)
            var boatId = await _db.Boats
                .Where(b => b.OwnerId == ownerId)
                .OrderBy(b => b.Id)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();

            if (boatId == Guid.Empty)
                throw new InvalidOperationException("No boat found for this owner.");

            // Pin çakışması kontrolü (opsiyonel ama yararlı)
            var pinInUse = await _db.Channels.AnyAsync(c => c.BoatId == boatId && c.Pin == dto.Pin);
            if (pinInUse)
                throw new InvalidOperationException($"Pin {dto.Pin} is already used on this boat.");

            var ent = new Channel
            {
                Id = Guid.NewGuid(),
                BoatId = boatId,
                Name = dto.Name,
                Type = dto.Type,
                Pin = dto.Pin,
                State = false // DTO'da InitialState yok, default false
            };

            _db.Channels.Add(ent);
            await _db.SaveChangesAsync();

            _logger.LogInformation("CHANNEL CREATE → ch={ChannelId} boat={BoatId} name={Name} pin={Pin}",
                ent.Id, ent.BoatId, ent.Name, ent.Pin);

            return new ChannelDto(ent.Id, ent.Name, ent.Type, ent.Pin, ent.State);
        }

        /// <summary>
        /// Kanal durumunu değiştirir ve aynı tekneye bağlı AKTİF ilk cihaza
        /// anında (MQTT push + DB queue) komut yollar.
        /// </summary>
        public async Task<ChannelDto> ToggleAsync(Guid ownerId, Guid channelId, ToggleChannelDto dto)
        {
            var ch = await _db.Channels
                .Include(c => c.Boat)
                .FirstOrDefaultAsync(c => c.Id == channelId && c.Boat != null && c.Boat.OwnerId == ownerId);

            if (ch is null)
                throw new KeyNotFoundException("Channel not found or not owned by user.");

            // Hedef cihaz: aynı boat'taki ilk aktif device (CreatedAt yoksa Id'ye göre)
            // aynı boat'taki ilk aktif device
            var deviceId = await _db.Devices
                .Where(d => d.BoatId == ch.BoatId && d.IsActive)
                .OrderBy(d => d.Id) // CreatedAt yoksa Id
                .Select(d => d.Id)
                .FirstOrDefaultAsync();

            if (deviceId == Guid.Empty)
                throw new InvalidOperationException("No active device for this boat.");

            // istenen state
            var desired = dto.State ?? !ch.State;
            ch.State = desired;
            await _db.SaveChangesAsync();

            // 🔵 tek cihaza push + queue
            await _cmd.EnqueueChannelSetAsync(deviceId, ch.Pin, desired, ch.Id);


            _logger.LogInformation("CHANNEL TOGGLE → ch={ChannelId} pin={Pin} state={State} device={DeviceId}",
                ch.Id, ch.Pin, desired, deviceId);

            return new ChannelDto(ch.Id, ch.Name, ch.Type, ch.Pin, ch.State);
        }
    }
}
