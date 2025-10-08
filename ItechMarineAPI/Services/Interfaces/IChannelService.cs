using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IChannelService
    {
        Task<IReadOnlyList<ChannelDto>> ListAsync(Guid ownerId);
        Task<ChannelDto> CreateAsync(Guid ownerId, ChannelCreateDto dto);
        Task<ChannelDto> ToggleAsync(Guid ownerId, Guid channelId, ToggleChannelDto dto);

    }
}
