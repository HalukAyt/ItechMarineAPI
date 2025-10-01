// Tests/CustomWebAppFactory.cs
using ItechMarineAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ItechMarineAPI.Tests;

public class CustomWebAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var desc = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (desc != null) services.Remove(desc);

            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("testdb"));
        });
    }
}
