using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Realtime;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ItechMarineAPI.Services
{
    public class ChannelService : IChannelService
    {
        private readonly AppDbContext _db;
        private readonly ICommandService _cmd;
        private readonly ILogger<ChannelService> _logger;
        private readonly IHubContext<BoatHub> _hub;

        public ChannelService(AppDbContext db, ICommandService cmd, IHubContext<BoatHub> hub, ILogger<ChannelService> logger)
        {
            _db = db;
            _cmd = cmd;
            _hub = hub;
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
                .FirstOrDefaultAsync(c => c.Id == channelId && c.Boat.OwnerId == ownerId);

            if (ch == null)
                throw new KeyNotFoundException("Channel not found or not owned by user.");

            ch.State = dto.State ?? !ch.State;
            await _db.SaveChangesAsync();

            // MQTT publish (cihaza komut gönder)
            await _cmd.EnqueueToBoatAsync(ch.BoatId, "channel.set", new
            {
                channelId = ch.Id,
                pin = ch.Pin,
                state = ch.State
            });

            // SignalR broadcast (uygulamada anında değişsin)
            await _hub.Clients.Group(ch.BoatId.ToString()).SendAsync("channel.state", new
            {
                channelId = ch.Id,
                pin = ch.Pin,
                state = ch.State
            });

            _logger.LogInformation(
                "CHANNEL TOGGLE ␦ ch={ChannelId} pin={Pin} state={State} boat={BoatId}",
                ch.Id, ch.Pin, ch.State, ch.BoatId);

            return new ChannelDto(ch.Id, ch.Name, ch.Type, ch.Pin, ch.State);
        }

    }
}
