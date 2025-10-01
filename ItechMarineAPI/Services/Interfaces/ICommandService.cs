using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces;

public interface ICommandService
{
    Task EnqueueToBoatAsync(Guid boatId, string type, object payload);
    Task<IReadOnlyList<CommandEnvelopeDto>> DequeueForDeviceAsync(Guid deviceId, int max = 10);
    Task AckAsync(Guid deviceId, long commandId);
}
