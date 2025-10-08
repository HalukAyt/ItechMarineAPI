using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Mqtt;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItechMarineAPI.Services
{
    public class CommandService : ICommandService
    {
        private readonly AppDbContext _db;
        private readonly IMqttPublisher _mqtt;
        private readonly ILogger<CommandService> _logger;

        // JSON ayarları tek noktada
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public CommandService(AppDbContext db, IMqttPublisher mqtt, ILogger<CommandService> logger)
        {
            _db = db;
            _mqtt = mqtt;
            _logger = logger;
        }

        /* =========================================================
         *  ENQUEUE: Tek cihaza ham (type/payload) komut
         * ========================================================= */
        public async Task<CommandEnvelopeDto> EnqueueRawAsync(Guid deviceId, string type, object payload)
        {
            var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);

            var cmd = new DeviceCommand
            {
                DeviceId = deviceId,
                Type = type,
                PayloadJson = payloadJson,
                CreatedAt = DateTime.UtcNow
            };

            _db.DeviceCommands.Add(cmd);
            await _db.SaveChangesAsync();

            await PublishMqttAsync(deviceId, cmd, retain: true); // 🔵 retained=true

            _logger.LogInformation("ENQUEUE → device={DeviceId} type={Type} id={Id}", deviceId, type, cmd.Id);
            return ToDto(cmd);
        }

        /* =========================================================
         *  ENQUEUE: Kanal set kısayolu (pin/state)
         * ========================================================= */
        public Task<CommandEnvelopeDto> EnqueueChannelSetAsync(Guid deviceId, int pin, bool state, Guid? channelId = null)
            => EnqueueRawAsync(deviceId, "channel.set", new { channelId, pin, state });

        /* =========================================================
         *  ENQUEUE: Teknedeki TÜM aktif cihazlara (gerekirse)
         * ========================================================= */
        public async Task EnqueueToBoatAsync(Guid boatId, string type, object payload)
        {
            var deviceIds = await _db.Devices
                .Where(d => d.BoatId == boatId && d.IsActive)
                .Select(d => d.Id)
                .ToListAsync();

            if (deviceIds.Count == 0)
            {
                _logger.LogWarning("ENQUEUE boat={BoatId} için aktif cihaz yok", boatId);
                return;
            }

            var payloadJson = JsonSerializer.Serialize(payload, JsonOpts);
            var now = DateTime.UtcNow;

            var rows = deviceIds.Select(did => new DeviceCommand
            {
                DeviceId = did,
                Type = type,
                PayloadJson = payloadJson,
                CreatedAt = now
            }).ToList();

            _db.DeviceCommands.AddRange(rows);
            await _db.SaveChangesAsync();

            // MQTT publish (retained=true → sonradan abone olsa da hemen alır)
            foreach (var cmd in rows)
                _ = PublishMqttAsync(cmd.DeviceId, cmd, retain: true);

            _logger.LogInformation("ENQUEUE boat={BoatId} type={Type} count={Count}", boatId, type, rows.Count);
        }

        /* =========================================================
         *  DEQUEUE: Cihaz HTTP pull ile alsın (fallback)
         * ========================================================= */
        public async Task<IReadOnlyList<CommandEnvelopeDto>> DequeueForDeviceAsync(Guid deviceId, int max = 10)
        {
            var list = await _db.DeviceCommands
                .Where(c => c.DeviceId == deviceId && c.DequeuedAt == null && c.AckedAt == null)
                .OrderBy(c => c.Id)
                .Take(max)
                .ToListAsync();

            if (list.Count == 0) return Array.Empty<CommandEnvelopeDto>();

            var now = DateTime.UtcNow;
            foreach (var c in list) c.DequeuedAt = now;

            await _db.SaveChangesAsync();

            _logger.LogDebug("DEQUEUE → device={DeviceId} count={Count}", deviceId, list.Count);
            return list.Select(ToDto).ToList();
        }

        /* =========================================================
         *  ACK: Cihaz uyguladı (ESP32 ack gönderir)
         * ========================================================= */
        public async Task AckAsync(Guid deviceId, long commandId)
        {
            var cmd = await _db.DeviceCommands
                .FirstOrDefaultAsync(c => c.Id == commandId && c.DeviceId == deviceId);

            if (cmd is null)
            {
                _logger.LogWarning("ACK NOT FOUND → device={DeviceId} cmdId={CmdId}", deviceId, commandId);
                return;
            }

            cmd.AckedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 🔵 Topic'teki retained mesajı temizle
            await _mqtt.PublishAsync($"itechmarine/device/{deviceId}/commands", "", qos: 0, retain: true);

            _logger.LogInformation("ACK ␦ device={DeviceId} cmdId={CmdId}", deviceId, commandId);
        }


        /* =========================================================
         *  Helper: MQTT publish (topic: itechmarine/device/{id}/commands)
         * ========================================================= */
        private async Task PublishMqttAsync(Guid deviceId, DeviceCommand cmd, bool retain = true)
        {
            try
            {
                var topic = $"itechmarine/device/{deviceId}/commands";

                var envelope = new
                {
                    id = cmd.Id,
                    type = cmd.Type,
                    payloadJson = cmd.PayloadJson,
                    createdAt = cmd.CreatedAt
                };

                var json = JsonSerializer.Serialize(envelope, JsonOpts);

                // 🔵 retained=true → cihaz sonradan abone olsa bile son komutu anında alır
                await _mqtt.PublishAsync(topic, json, qos: 0, retain: true);


                // Log'u INFO seviyesinde tutalım ki kesin görünsün
                _logger.LogInformation("MQTT PUSH → {Topic} : {Json}", topic, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MQTT publish başarısız (device={DeviceId}, cmdId={CmdId})", deviceId, cmd.Id);
            }
        }

        private static CommandEnvelopeDto ToDto(DeviceCommand c)
            => new(c.Id, c.Type, c.PayloadJson, c.CreatedAt);
    }
}
