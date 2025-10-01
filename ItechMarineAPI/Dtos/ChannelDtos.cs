using ItechMarineAPI.Entities;

namespace ItechMarineAPI.Dtos
{
    public record ChannelCreateDto(string Name, ChannelType Type, int Pin);
    public record ChannelDto(Guid Id, string Name, ChannelType Type, int Pin, bool State);
    public record ToggleChannelDto(bool? State); // null ise toggle, true/false ise set
}
