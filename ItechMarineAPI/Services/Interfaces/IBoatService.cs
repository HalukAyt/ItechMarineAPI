using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IBoatService
    {
        Task<BoatDto> GetMyBoatAsync(Guid ownerId);
        Task<BoatDto> CreateAsync(Guid ownerId, BoatCreateDto dto);
    }
}
