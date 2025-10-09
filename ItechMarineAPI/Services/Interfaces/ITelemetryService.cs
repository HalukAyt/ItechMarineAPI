using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface ITelemetryService
    {
        // Listeleme (mevcut)
        Task<PagedResult<TelemetryItemDto>> QueryAsync(Guid ownerId, TelemetryQueryDto q);

        // Ingest/Save: cihazdan gelen tek telemetri kaydı
        Task SaveAsync(Guid deviceId, string key, string value, DateTime? timestampUtc = null);
    }
}
