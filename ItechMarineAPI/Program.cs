using System.Threading.RateLimiting;
using ItechMarineAPI.Data;
using ItechMarineAPI.Entities;
using ItechMarineAPI.Realtime;
using ItechMarineAPI.Security; // JwtConfig burada
using ItechMarineAPI.Services.Interfaces;
using ItechMarineAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using ItechMarineAPI.Mqtt;


var builder = WebApplication.CreateBuilder(args);
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();
// -------------------- Configuration --------------------
var cfg = builder.Configuration;

// -------------------- Database (PostgreSQL) --------------------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(cfg.GetConnectionString("DefaultConnection")));

// -------------------- Identity (Guid keys) --------------------
builder.Services.AddIdentityCore<AppUser>(opt =>
{
    opt.Password.RequiredLength = 8;
    opt.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<AppDbContext>()
.AddSignInManager();

// -------------------- JWT Authentication --------------------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = JwtConfig.BuildValidation(cfg);

        // (Opsiyonel) SignalR'ın query string'den token alabilmesi için
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/boat"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("OwnerOnly", p => p.RequireRole("BoatOwner"));
});
// -------------------- DI: Mqtt --------------------
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<MqttBridgeService>();
builder.Services.AddSingleton<IMqttPublisher>(sp => sp.GetRequiredService<MqttBridgeService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttBridgeService>());



// -------------------- Data Protection (DeviceKeyProtected) --------------------
builder.Services.AddDataProtection();
builder.Services.AddSingleton<IProtectionService, ProtectionService>();

// -------------------- DI: Services --------------------
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBoatService, BoatService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IChannelService, ChannelService>();
builder.Services.AddScoped<ICommandService, CommandService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();

// -------------------- Controllers --------------------
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // İstersen camelCase/enum string ayarları burada
        // o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        // o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// -------------------- SignalR --------------------
builder.Services.AddSignalR();

// -------------------- CORS (Expo/Web istemcileri için) --------------------
builder.Services.AddCors(o => o.AddPolicy("app", p =>
{
    p.WithOrigins(
        "http://localhost:19006", // Expo Go (web)
        "http://localhost:8081",  // Metro bundler
        "http://localhost:5173"   // Vite/React
                                  // Prod origin(ler)ini burada ekleyebilirsin
    )
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials();
}));

// -------------------- Rate Limiting --------------------
builder.Services.AddRateLimiter(o =>
{
    // IP başına dakikada 100 istek (global)
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "anon";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    o.AddPolicy("device-rl", httpContext =>
    {
        // Route'tan DeviceId çek
        var routeVals = httpContext.GetRouteData()?.Values;
        var key = routeVals != null && routeVals.TryGetValue("deviceId", out var val) ? val?.ToString() : "unknown";
        if (string.IsNullOrWhiteSpace(key)) key = "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,             // cihaz başına 60 istek / dakika
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });
    o.RejectionStatusCode = 429;

});

// -------------------- Swagger + Bearer --------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MarineControl API", Version = "v1" });

    var securitySchema = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Bearer. Örnek: **Bearer {token}**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", securitySchema);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securitySchema, Array.Empty<string>() }
    });
});

builder.WebHost.UseUrls("http://0.0.0.0:5087");

// ==========================================================
// -------------------- Build & Middleware ------------------
// ==========================================================
var app = builder.Build();

// (Opsiyonel) DB migrations'ı otomatik uygulamak istersen:
/*
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
*/

app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});
// Swagger
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS redirect
//app.UseHttpsRedirection();

// CORS
app.UseCors("app");

// RateLimiter
app.UseRateLimiter();

// Auth
app.UseAuthentication();
app.UseAuthorization();

// Controllers
app.MapControllers();

// SignalR Hub (owner rolü gerekli)
app.MapHub<BoatHub>("/hubs/boat").RequireAuthorization("OwnerOnly");

// Seed (rol ve demo kullanıcı/tekne)
await Seed.EnsureSeedAsync(app.Services);

// Run
app.Run();

public partial class Program { }