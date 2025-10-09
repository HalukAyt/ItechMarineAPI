using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ItechMarineAPI.Realtime
{
    [Authorize(Roles = "BoatOwner")]
    public class BoatHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var uid = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            await base.OnConnectedAsync();
        }

        // İstemci tekne grubuna katılır (method adı: JoinBoat)
        public Task JoinBoat(string boatId)
        {
            if (string.IsNullOrWhiteSpace(boatId))
                throw new HubException("boatId is required");
            return Groups.AddToGroupAsync(Context.ConnectionId, boatId);
        }

        public Task LeaveBoat(string boatId)
        {
            if (string.IsNullOrWhiteSpace(boatId))
                throw new HubException("boatId is required");
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, boatId);
        }

        public Task<string> Ping() => Task.FromResult("pong");
    }
}
    