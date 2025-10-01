// Services/TelemetryService.cs
using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Services;

public class TelemetryService : ITelemetryService
{
    private readonly AppDbContext _db;
    public TelemetryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<TelemetryItemDto>> QueryAsync(Guid ownerId, TelemetryQueryDto q)
    {
        var boatId = await _db.Boats.Where(b => b.OwnerId == ownerId).Select(b => b.Id).FirstAsync();

        var query = _db.Telemetries.AsNoTracking().Where(t => t.BoatId == boatId);

        if (q.FromUtc is not null) query = query.Where(t => t.CreatedAt >= q.FromUtc);
        if (q.ToUtc is not null) query = query.Where(t => t.CreatedAt <= q.ToUtc);
        if (q.DeviceId is not null) query = query.Where(t => t.DeviceId == q.DeviceId);
        if (q.Keys is { Length: > 0 }) query = query.Where(t => q.Keys!.Contains(t.Key));

        var total = await query.LongCountAsync();
        var page = q.Page <= 0 ? 1 : q.Page;
        var size = Math.Clamp(q.PageSize, 1, 1000);

        var items = await query
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((page - 1) * size).Take(size)
            .Select(t => new TelemetryItemDto(t.Id, t.BoatId, t.DeviceId, t.Key, t.Value, t.CreatedAt))
            .ToListAsync();

        return new PagedResult<TelemetryItemDto>(page, size, total, items);
    }
}
