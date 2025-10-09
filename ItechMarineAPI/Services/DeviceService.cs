using System.Security.Cryptography;
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Security;          // IProtectionService
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ItechMarineAPI.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly AppDbContext _db;
        private readonly IProtectionService _ps;
        private readonly ILogger<DeviceService> _log;

        public DeviceService(AppDbContext db, IProtectionService ps, ILogger<DeviceService> log)
        {
            _db = db;
            _ps = ps;
            _log = log;
        }

        // =========================================================
        // Helpers
        // =========================================================

        private static string GeneratePlainDeviceKey(int bytes = 24)
        {
            Span<byte> buff = stackalloc byte[bytes];
            RandomNumberGenerator.Fill(buff);
            return Convert.ToBase64String(buff); // ~32 karakter
        }

        private static DeviceDto ToDto(Device d) =>
            new DeviceDto(
                d.Id,
                d.Name,
                d.IsActive,
                d.IsOnline,
                d.LastSeenUtc,
                d.BoatId
            );

        private async Task<Guid> ResolveBoatIdAsync(Guid ownerId, Guid? fromDtoBoatId)
        {
            if (fromDtoBoatId.HasValue && fromDtoBoatId.Value != Guid.Empty)
            {
                var exists = await _db.Boats.AnyAsync(b => b.Id == fromDtoBoatId.Value && b.OwnerId == ownerId);
                if (!exists)
                    throw new InvalidOperationException("Boat not found or not owned by user.");
                return fromDtoBoatId.Value;
            }

            var boatId = await _db.Boats
                .Where(b => b.OwnerId == ownerId)
                .OrderBy(b => b.Id)
                .Select(b => b.Id)
                .FirstOrDefaultAsync();

            if (boatId == Guid.Empty)
                throw new InvalidOperationException("No boat found for this owner.");

            return boatId;
        }

        // =========================================================
        // IDeviceService Implementation
        // =========================================================

        /// <summary>
        /// Yeni cihaz oluşturur, DeviceKeyProtected ile veritabanına kaydeder
        /// ve plain key'i sadece dönen response içinde verir.
        /// </summary>
        public async Task<(DeviceDto device, RotateKeyResponseDto? key)> CreateAsync(Guid ownerId, DeviceCreateDto dto)
        {
            Guid boatId = await ResolveBoatIdAsync(
                ownerId,
                dto.GetType().GetProperty("BoatId")?.GetValue(dto) as Guid?
            );

            // 🔑 Plain key oluştur
            string plain = GeneratePlainDeviceKey();
            string protectedKey = _ps.ProtectDeviceKey(plain);

            var dev = new Device
            {
                Id = Guid.NewGuid(),
                BoatId = boatId,
                Name = string.IsNullOrWhiteSpace(dto.Name) ? "ESP32" : dto.Name.Trim(),
                DeviceKeyProtected = protectedKey,
                IsActive = true,
                IsOnline = false
            };

            _db.Devices.Add(dev);
            await _db.SaveChangesAsync();

            _log.LogInformation("DEVICE CREATE → id={DeviceId} boat={BoatId}", dev.Id, dev.BoatId);

            var deviceDto = ToDto(dev);
            var key = new RotateKeyResponseDto(plain);
            return (deviceDto, key);
        }

        /// <summary>
        /// Kullanıcının tüm cihazlarını listeler.
        /// </summary>
        public async Task<IEnumerable<DeviceDto>> ListAsync(Guid ownerId)
        {
            var items = await _db.Devices
                .Include(d => d.Boat)
                .Where(d => d.Boat != null && d.Boat.OwnerId == ownerId)
                .OrderBy(d => d.Name)
                .ToListAsync();

            return items.Select(ToDto);
        }

        /// <summary>
        /// Mevcut bir cihazın anahtarını yeniler (plain + protected).
        /// </summary>
        public async Task<RotateKeyResponseDto> RotateKeyAsync(Guid ownerId, Guid deviceId)
        {
            var dev = await _db.Devices
                .Include(d => d.Boat)
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.Boat != null && d.Boat.OwnerId == ownerId);

            if (dev is null)
                throw new KeyNotFoundException("Device not found or not owned by user.");

            string plain = GeneratePlainDeviceKey();
            string protectedKey = _ps.ProtectDeviceKey(plain);

            dev.DeviceKeyProtected = protectedKey;
            await _db.SaveChangesAsync();

            _log.LogInformation("DEVICE ROTATE KEY → id={DeviceId}", dev.Id);

            return new RotateKeyResponseDto(plain);
        }
    }
}
