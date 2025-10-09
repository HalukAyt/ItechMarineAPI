using ItechMarineAPI.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ItechMarineAPI.Data;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Boat> Boats => Set<Boat>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Telemetry> Telemetries => Set<Telemetry>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<DeviceCommand> DeviceCommands => Set<DeviceCommand>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Boat>()
            .HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Device>(e =>
        {
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.Property(x => x.IsOnline).HasDefaultValue(false); // 🔵
        });

        b.Entity<Channel>()
            .HasOne(x => x.Boat)
            .WithMany(x => x.Channels)
            .HasForeignKey(x => x.BoatId);

        b.Entity<Telemetry>().HasIndex(x => new { x.BoatId, x.CreatedAt });
        b.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.Entity<DeviceCommand>()
        .HasOne(x => x.Device).WithMany()
        .HasForeignKey(x => x.DeviceId);

        b.Entity<DeviceCommand>().HasIndex(x => new { x.DeviceId, x.CreatedAt });
    }
}
