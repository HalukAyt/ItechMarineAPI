using ItechMarineAPI.Dtos;
using ItechMarineAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ItechMarineAPI.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register-owner")]
    public async Task<ActionResult<AuthResponseDto>> RegisterOwner([FromBody] RegisterOwnerDto dto)
        => Ok(await _auth.RegisterOwnerAsync(dto));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginDto dto)
        => Ok(await _auth.LoginAsync(dto));

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponseDto>> Refresh([FromBody] RefreshRequestDto dto)
        => Ok(await _auth.RefreshAsync(dto));
}
