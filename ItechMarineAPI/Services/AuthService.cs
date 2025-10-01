using Microsoft.AspNetCore.Identity;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Services.Interfaces;
using System;
using ItechMarineAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _um;
    private readonly SignInManager<AppUser> _sm;
    private readonly ITokenService _ts;
    private readonly AppDbContext _db;

    public AuthService(UserManager<AppUser> um, SignInManager<AppUser> sm, ITokenService ts, AppDbContext db)
    {
        _um = um; _sm = sm; _ts = ts; _db = db;
    }

    public async Task<AuthResponseDto> RegisterOwnerAsync(RegisterOwnerDto dto)
    {
        var user = new AppUser { UserName = dto.Email, Email = dto.Email };
        var res = await _um.CreateAsync(user, dto.Password);
        if (!res.Succeeded) throw new InvalidOperationException(string.Join(";", res.Errors.Select(e => e.Description)));

        await _um.AddToRoleAsync(user, "BoatOwner");

        var boat = new Boat { Name = dto.BoatName, OwnerId = user.Id };
        _db.Boats.Add(boat);

        // Refresh token
        var (rt, plainRt) = RefreshToken.Create(user.Id, TimeSpan.FromDays(30));
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        var (token, exp) = _ts.CreateAccessToken(user, boat.Id);
        return new AuthResponseDto(token, exp, plainRt, boat.Id);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var user = await _um.Users.FirstOrDefaultAsync(x => x.Email == dto.Email);
        if (user == null) throw new UnauthorizedAccessException();

        var ok = await _sm.CheckPasswordSignInAsync(user, dto.Password, false);
        if (!ok.Succeeded) throw new UnauthorizedAccessException();

        var boatId = await _db.Boats.Where(b => b.OwnerId == user.Id).Select(b => b.Id).FirstAsync();
        var (rt, plainRt) = RefreshToken.Create(user.Id, TimeSpan.FromDays(30));
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        var (token, exp) = _ts.CreateAccessToken(user, boatId);
        return new AuthResponseDto(token, exp, plainRt, boatId);
    }

    public async Task<AuthResponseDto> RefreshAsync(RefreshRequestDto dto)
    {
        var hash = RefreshToken.Hash(dto.RefreshToken);
        var saved = await _db.RefreshTokens.FirstOrDefaultAsync(x =>
            x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > DateTime.UtcNow);
        if (saved == null) throw new UnauthorizedAccessException();

        var user = await _um.FindByIdAsync(saved.UserId.ToString()) ?? throw new UnauthorizedAccessException();
        var boatId = await _db.Boats.Where(b => b.OwnerId == user.Id).Select(b => b.Id).FirstAsync();

        // rotate
        saved.RevokedAt = DateTime.UtcNow;
        var (rt, plainRt) = RefreshToken.Create(user.Id, TimeSpan.FromDays(30));
        _db.RefreshTokens.Add(rt);
        await _db.SaveChangesAsync();

        var (token, exp) = _ts.CreateAccessToken(user, boatId);
        return new AuthResponseDto(token, exp, plainRt, boatId);
    }
}
