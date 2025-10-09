using System.Security.Claims;
using ItechMarineAPI.Dtos;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Authorize(Roles = "BoatOwner")]
[Route("boats")]
public class BoatsController : ControllerBase
{
    private readonly IBoatService _boats;
    private readonly IChannelService _channels;
    public BoatsController(IBoatService boats, IChannelService channels)
    { _boats = boats; _channels = channels; }

    private Guid OwnerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("me")]
    public async Task<ActionResult<BoatDto>> GetMyBoat()
        => Ok(await _boats.GetMyBoatAsync(OwnerId));

    [HttpPost("channels")]
    public async Task<ActionResult<ChannelDto>> CreateChannel([FromBody] ChannelCreateDto dto)
        => Ok(await _channels.CreateAsync(OwnerId, dto));

    [HttpGet("channels")]
    public async Task<ActionResult<IEnumerable<ChannelDto>>> ListChannels()
        => Ok(await _channels.ListAsync(OwnerId));

    [HttpGet("status")]
    public async Task<ActionResult<BoatStatusDto>> GetStatus()
    => Ok(await _boats.GetStatusAsync(OwnerId));
}
