using ItechMarineAPI.Dtos;

public interface ICommandService
{
    Task<CommandEnvelopeDto> EnqueueRawAsync(Guid deviceId, string type, object payload);
    Task<CommandEnvelopeDto> EnqueueChannelSetAsync(Guid deviceId, int pin, bool state, Guid? channelId = null);
    Task EnqueueToBoatAsync(Guid boatId, string type, object payload);
    Task<IReadOnlyList<CommandEnvelopeDto>> DequeueForDeviceAsync(Guid deviceId, int max = 10);
    Task AckAsync(Guid deviceId, long commandId);
}
