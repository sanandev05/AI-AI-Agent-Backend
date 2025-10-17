using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AI_AI_Agent.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _cfg;
    public AuthController(IConfiguration cfg) => _cfg = cfg;

    public sealed record LoginRequest(string Username, string Password);
    public sealed record LoginResponse(string Token, DateTime ExpiresAtUtc);

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest("username and password required");

        // NOTE: Replace this with real user validation. For now, accept any non-empty credentials.
        var key = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
        var issuer = _cfg["Jwt:Issuer"] ?? "ai-agent";
        var audience = _cfg["Jwt:Audience"] ?? "ai-agent-clients";
        var minutes = int.TryParse(_cfg["Jwt:ExpiryMinutes"], out var m) ? m : 60;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, req.Username),
            new("name", req.Username),
            new("role", "user")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return Ok(new LoginResponse(token, expires));
    }
}
