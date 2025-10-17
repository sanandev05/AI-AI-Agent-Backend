using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AI_AI_Agent.API.JWT
{
    public class JwtTokenGenerator
    {
        private readonly IConfiguration _config;

        public JwtTokenGenerator(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateToken(IdentityUser user)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                // Also add common ASP.NET Core identity claim types for compatibility in controllers
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id)
            };

            var keyStr = _config["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(keyStr))
            {
                throw new InvalidOperationException("JWT key (Jwt:Key) is not configured. Set it in appsettings or user-secrets.");
            }
            var keyBytes = Encoding.UTF8.GetBytes(keyStr);
            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException($"JWT key (Jwt:Key) is too short for HS256. It must be at least 256 bits (32 bytes). Current: {keyBytes.Length * 8} bits. Provide a longer random secret (32+ characters).");
            }
            var key = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Read expiry in minutes (default 60)
            var expireMinutesStr = _config["Jwt:ExpireMinutes"]; 
            if (!double.TryParse(expireMinutesStr, out var expireMinutes) || expireMinutes <= 0)
            {
                expireMinutes = 60;
            }

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}

