using ItechMarineAPI.Data;
using ItechMarineAPI.Entities;
using Microsoft.AspNetCore.Identity;

namespace ItechMarineAPI.Data;

public static class Seed
{
    public static async Task EnsureSeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var roleMgr = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userMgr = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!await roleMgr.RoleExistsAsync("BoatOwner"))
            await roleMgr.CreateAsync(new IdentityRole<Guid>("BoatOwner"));

        // Opsiyonel: demo kullanıcı
        if (await userMgr.FindByEmailAsync("owner@demo.test") is null)
        {
            var u = new AppUser { UserName = "owner@demo.test", Email = "owner@demo.test" };
            await userMgr.CreateAsync(u, "DemoPass!123");
            await userMgr.AddToRoleAsync(u, "BoatOwner");

            var boat = new Boat { Name = "Demo Boat", OwnerId = u.Id };
            db.Boats.Add(boat);
            await db.SaveChangesAsync();
        }
    }
}
