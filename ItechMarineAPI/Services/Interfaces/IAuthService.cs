using ItechMarineAPI.Dtos;

namespace ItechMarineAPI.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponseDto> RegisterOwnerAsync(RegisterOwnerDto dto);
        Task<AuthResponseDto> LoginAsync(LoginDto dto);
        Task<AuthResponseDto> RefreshAsync(RefreshRequestDto dto);
    }
}
