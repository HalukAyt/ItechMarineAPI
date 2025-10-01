using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ItechMarineAPI.Realtime;

[Authorize(Roles = "BoatOwner")]
public class BoatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var boatId = Context.User?.Claims.FirstOrDefault(c => c.Type == "boatId")?.Value;
        if (!string.IsNullOrWhiteSpace(boatId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"boat:{boatId}");
        await base.OnConnectedAsync();
    }
}
