using System.Security.Claims;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Authorize(Roles = "BoatOwner")]
[Route("channels")]
public class ChannelsController : ControllerBase
{
    private readonly IChannelService _channels;
    public ChannelsController(IChannelService channels) => _channels = channels;

    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpPost("{channelId:guid}/toggle")]
    public async Task<ActionResult<ChannelDto>> Toggle(Guid channelId, [FromBody] ToggleChannelDto dto)
        => Ok(await _channels.ToggleAsync(OwnerId, channelId, dto));
}
