using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IChannelService
    {
        Task<ChannelDto> CreateAsync(Guid ownerId, ChannelCreateDto dto);
        Task<IEnumerable<ChannelDto>> ListAsync(Guid ownerId);
        Task<ChannelDto> ToggleAsync(Guid ownerId, Guid channelId, ToggleChannelDto dto);
    }
}
