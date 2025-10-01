namespace ItechMarineAPI.Dtos
{
    public record RegisterOwnerDto(string Email, string Password, string BoatName);
    public record LoginDto(string Email, string Password);
    public record AuthResponseDto(string Token, DateTime ExpiresAt, string RefreshToken, Guid BoatId);
    public record RefreshRequestDto(string RefreshToken);
}
