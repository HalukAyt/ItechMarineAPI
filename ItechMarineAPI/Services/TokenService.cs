using ItechMarineAPI.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ItechMarineAPI.Services
{
    public interface ITokenService
    {
        (string token, DateTime exp) CreateAccessToken(AppUser user, Guid boatId);
    }
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _cfg;
        public TokenService(IConfiguration cfg) => _cfg = cfg;

        public (string token, DateTime exp) CreateAccessToken(AppUser user, Guid boatId)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.Role, "BoatOwner"),
            new("boatId", boatId.ToString())
        };
            var exp = DateTime.UtcNow.AddMinutes(30);
            var token = new JwtSecurityToken(_cfg["Jwt:Issuer"], _cfg["Jwt:Audience"], claims,
                expires: exp, signingCredentials: creds);
            return (new JwtSecurityTokenHandler().WriteToken(token), exp);
        }
    }
}
