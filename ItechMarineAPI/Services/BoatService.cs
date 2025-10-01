using ItechMarineAPI.Data;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;

namespace ItechMarineAPI.Services;

public class BoatService : IBoatService
{
    private readonly AppDbContext _db;
    public BoatService(AppDbContext db) => _db = db;

    public async Task<BoatDto> GetMyBoatAsync(Guid ownerId)
    {
        var boat = await _db.Boats.AsNoTracking().FirstOrDefaultAsync(b => b.OwnerId == ownerId)
                   ?? throw new InvalidOperationException("Boat not found");
        return new BoatDto(boat.Id, boat.Name);
    }

    public async Task<BoatDto> CreateAsync(Guid ownerId, BoatCreateDto dto)
    {
        var boat = new Boat { Name = dto.Name, OwnerId = ownerId };
        _db.Boats.Add(boat);
        await _db.SaveChangesAsync();
        return new BoatDto(boat.Id, boat.Name);
    }
}
