using System.Globalization;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Realtime;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Services
{
    public class TelemetryService : ITelemetryService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<BoatHub> _hub;
        private readonly ILogger<TelemetryService> _logger;

        public TelemetryService(AppDbContext db, IHubContext<BoatHub> hub, ILogger<TelemetryService> logger)
        {
            _db = db;
            _hub = hub;
            _logger = logger;
        }

        /* ================================
         *  QUERY (mevcut)
         * ================================ */
        public async Task<PagedResult<TelemetryItemDto>> QueryAsync(Guid ownerId, TelemetryQueryDto q)
        {
            var boatId = await _db.Boats
                .Where(b => b.OwnerId == ownerId)
                .Select(b => b.Id)
                .FirstAsync();

            var query = _db.Telemetries.AsNoTracking().Where(t => t.BoatId == boatId);

            if (q.FromUtc is not null) query = query.Where(t => t.CreatedAt >= q.FromUtc);
            if (q.ToUtc is not null) query = query.Where(t => t.CreatedAt <= q.ToUtc);
            if (q.DeviceId is not null) query = query.Where(t => t.DeviceId == q.DeviceId);
            if (q.Keys is { Length: > 0 }) query = query.Where(t => q.Keys!.Contains(t.Key));

            var total = await query.LongCountAsync();
            var page = q.Page <= 0 ? 1 : q.Page;
            var size = Math.Clamp(q.PageSize, 1, 1000);

            var items = await query
                .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
                .Skip((page - 1) * size).Take(size)
                .Select(t => new TelemetryItemDto(t.Id, t.BoatId, t.DeviceId, t.Key, t.Value, t.CreatedAt))
                .ToListAsync();

            return new PagedResult<TelemetryItemDto>(page, size, total, items);
        }

        /* ================================
         *  SAVE / INGEST (kural + alert)
         * ================================ */
        public async Task SaveAsync(Guid deviceId, string key, string value, DateTime? timestampUtc = null)
        {
            // Cihaz ve tekne bilgisi
            var dev = await _db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == deviceId);

            if (dev is null)
                throw new KeyNotFoundException("Device not found.");

            var createdAt = timestampUtc ?? DateTime.UtcNow;

            // DB kaydet
            var row = new Entities.Telemetry
            {
                BoatId = dev.BoatId,
                DeviceId = deviceId,
                Key = key,
                Value = value,
                CreatedAt = createdAt
            };

            _db.Telemetries.Add(row);
            await _db.SaveChangesAsync();

            // ---- Basit demo kuralı (senin istediğin satırlar) ----
            // battery.voltage < 11.8 ise alert yayınla
            if (key == "battery.voltage" &&
                double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) &&
                v < 11.8)
            {
                await _hub.Clients.Group(dev.BoatId.ToString()).SendAsync("alert", new
                {
                    level = "warning",
                    title = "Low battery voltage",
                    message = $"Voltage {v:F2}V",
                    key,
                    value,
                    at = DateTime.UtcNow
                });

                _logger.LogWarning("ALERT ␦ Low battery voltage {Voltage}V boat={BoatId} device={DeviceId}",
                    v, dev.BoatId, deviceId);
            }
        }
    }
}
