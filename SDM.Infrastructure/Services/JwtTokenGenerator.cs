using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SDM.Application.Interfaces;
using SDM.Application.Settings;
using SDM.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SDM.Infrastructure.Services
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtSettings _jwt;

        public JwtTokenGenerator(
            IOptions<JwtSettings> options)
        {
            _jwt = options.Value;
        }

        public string GenerateToken(User user)
        {
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),

            new(ClaimTypes.Email, user.Email),

            new(ClaimTypes.Role, user.Role.Name)
        };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwt.Key));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

        // Generate a device-specific token with custom expiry (minutes)
        public string GenerateDeviceToken(User user, int expiryMinutes)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.Role, "Device"),
                new(ClaimTypes.Name, user.Email)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwt.Key));

            var creds = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }

    }
}
