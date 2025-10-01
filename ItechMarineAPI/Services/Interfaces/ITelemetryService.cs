// Services/Interfaces/ITelemetryService.cs
using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces;
public interface ITelemetryService
{
    Task<PagedResult<TelemetryItemDto>> QueryAsync(Guid ownerId, TelemetryQueryDto q);
}
