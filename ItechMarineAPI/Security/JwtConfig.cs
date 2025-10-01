using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ItechMarineAPI.Security
{
    public static class JwtConfig
    {
        public static TokenValidationParameters BuildValidation(IConfiguration cfg) => new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidAudience = cfg["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
}
