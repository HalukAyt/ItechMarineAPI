using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IDeviceService
    {
        Task<(DeviceDto Device, RotateKeyResponseDto? Key)> CreateAsync(Guid ownerId, DeviceCreateDto dto);
        Task<IEnumerable<DeviceDto>> ListAsync(Guid ownerId);
        Task<RotateKeyResponseDto> RotateKeyAsync(Guid ownerId, Guid deviceId);
    }
}
