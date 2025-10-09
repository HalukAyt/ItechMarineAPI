using System.Security.Claims;
using ItechMarineAPI.Data;
using ItechMarineAPI.Realtime;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Authorize(Roles = "BoatOwner")]
[Route("scenes")]
public class ScenesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICommandService _cmd;
    private readonly IHubContext<BoatHub> _hub;
    private readonly ILogger<ScenesController> _log;

    public ScenesController(AppDbContext db, ICommandService cmd, IHubContext<BoatHub> hub, ILogger<ScenesController> log)
    { _db = db; _cmd = cmd; _hub = hub; _log = log; }

    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Basit sahneler: isim → pin/state listesi
    // Not: Örnek olarak isim üzerinden filtre; istersen DB tablosu ile parametrik yaparız.
    private static List<(int pin, bool state)> BuildScene(string sceneName, IEnumerable<Entities.Channel> channels)
    {
        var name = sceneName.Trim().ToLowerInvariant();

        return name switch
        {
            // Hepsini kapat
            "alloff" => channels.Select(c => (c.Pin, false)).ToList(),

            // Hepsini aç
            "allon" => channels.Select(c => (c.Pin, true)).ToList(),

            // "Güverte" geçenleri aç, diğerlerine dokunma
            "anchorage" => channels
                .Where(c => c.Name.Contains("Güverte", StringComparison.OrdinalIgnoreCase))
                .Select(c => (c.Pin, true))
                .ToList(),

            // Örnek: Sadece “Kabın” (Kabin) geçenleri aç
            "cabin" => channels
                .Where(c => c.Name.Contains("Kabin", StringComparison.OrdinalIgnoreCase))
                .Select(c => (c.Pin, true))
                .ToList(),

            _ => throw new ArgumentException("Unknown scene name")
        };
    }

    [HttpPost("{scene}")]
    public async Task<ActionResult<object>> Run(string scene)
    {
        // 1) Tekneyi ve kanalları çek
        var boat = await _db.Boats.FirstOrDefaultAsync(b => b.OwnerId == OwnerId);
        if (boat is null) return NotFound("Boat not found");

        var channels = await _db.Channels.Where(c => c.BoatId == boat.Id).ToListAsync();
        if (channels.Count == 0) return Ok(new { scene, count = 0, message = "No channels." });

        // 2) Aksiyonları üret
        List<(int pin, bool state)> actions;
        try
        {
            actions = BuildScene(scene, channels);
        }
        catch
        {
            return BadRequest(new { scene, message = "Unknown scene" });
        }

        if (actions.Count == 0) return Ok(new { scene, count = 0, message = "No-op." });

        // 3) DB state güncelle (UI anında doğru görsün)
        foreach (var (pin, state) in actions)
        {
            var ch = channels.FirstOrDefault(x => x.Pin == pin);
            if (ch != null) ch.State = state;
        }
        await _db.SaveChangesAsync();

        // 4) Cihaza komutları gönder (boat'taki tüm cihazlara)
        foreach (var (pin, state) in actions)
            await _cmd.EnqueueToBoatAsync(boat.Id, "channel.set", new { pin, state });

        // 5) SignalR ile anında UI güncelle
        foreach (var ch in channels)
        {
            await _hub.Clients.Group(boat.Id.ToString()).SendAsync("channel.state", new
            {
                channelId = ch.Id,
                pin = ch.Pin,
                state = ch.State
            });
        }

        _log.LogInformation("SCENE ␦ {Scene} boat={BoatId} actions={Count}", scene, boat.Id, actions.Count);
        return Ok(new { scene, count = actions.Count });
    }
}
