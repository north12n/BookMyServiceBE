using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BookMyService.Models;
using BookMyServiceBE.Repository.IRepository;
using Microsoft.IdentityModel.Tokens;

namespace BookMyServiceBE.Repository
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _cfg;

        public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

        public string CreateToken(User user)
        {
            var key = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
            var issuer = _cfg["Jwt:Issuer"] ?? "BookMyServiceBE";
            var audience = _cfg["Jwt:Audience"] ?? "BookMyServiceFE";
            var expiresMinutes = int.TryParse(_cfg["Jwt:ExpiresMinutes"], out var m) ? m : 10080;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Role, user.UserRole.ToString()),
                new("role", user.UserRole.ToString()),
                new("fullName", user.FullName)
            };

            var creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}