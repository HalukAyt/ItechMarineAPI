using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IDeviceService
    {
        /// <summary>
        /// Yeni cihaz oluşturur. Plain anahtarı sadece bu cevapta döndürür.
        /// </summary>
        Task<(DeviceDto device, RotateKeyResponseDto? key)> CreateAsync(Guid ownerId, DeviceCreateDto dto);

        /// <summary>
        /// Kullanıcıya ait cihaz listesini döndürür.
        /// </summary>
        Task<IEnumerable<DeviceDto>> ListAsync(Guid ownerId);

        /// <summary>
        /// Cihaz anahtarını yeniler ve yeni plain key'i döndürür.
        /// </summary>
        Task<RotateKeyResponseDto> RotateKeyAsync(Guid ownerId, Guid deviceId);
    }
}
