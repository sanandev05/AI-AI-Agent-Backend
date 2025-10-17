using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using AI_AI_Agent.API.Hubs;
using AI_AI_Agent.Contract.Services;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AI_AI_Agent.API.Controllers
{
    [Route("api/diagnostics")]
    [ApiController]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IHubContext<AgentEventsHub> _hubContext;
        private readonly IGoogleSearchService _searchService;

        public DiagnosticsController(IConfiguration config, IHubContext<AgentEventsHub> hubContext, IGoogleSearchService searchService)
        {
            _config = config;
            _hubContext = hubContext;
            _searchService = searchService;
        }

        [HttpGet("jwt")] // unprotected on purpose; remove in production
        public IActionResult Jwt()
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expire = _config["Jwt:ExpireMinutes"];

            var keyLen = string.IsNullOrWhiteSpace(key) ? 0 : Encoding.UTF8.GetBytes(key).Length;

            return Ok(new
            {
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                issuer,
                audience,
                expire,
                hasKey = !string.IsNullOrWhiteSpace(key),
                keyBytes = keyLen
            });
        }

        [Authorize]
        [HttpGet("whoami")]
        public IActionResult WhoAmI()
        {
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var nameId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var subject = User.FindFirst("sub")?.Value;
            
            return Ok(new
            {
                authenticated = User.Identity?.IsAuthenticated ?? false,
                nameIdentifier = nameId,
                sub = subject,
                name = User.Identity?.Name,
                claims,
                headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString())
            });
        }

        [Authorize]
        [HttpPost("test-signalr/{chatId}")]
        public async Task<IActionResult> TestSignalR(string chatId, [FromBody] TestMessageRequest request)
        {
            try
            {
                await _hubContext.Clients.Group(chatId).SendAsync("test:message", new 
                { 
                    chatId, 
                    message = request.Message ?? "Test message from diagnostics controller",
                    timestamp = DateTime.UtcNow,
                    sender = User.Identity?.Name ?? "System"
                });

                return Ok(new { success = true, message = "Test message sent to SignalR group", chatId });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("test-auth")]
        public IActionResult TestAuth()
        {
            return Ok(new 
            { 
                message = "Authorization working", 
                user = User.Identity?.Name ?? "Anonymous",
                isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                timestamp = DateTime.UtcNow
            });
        }

        [HttpGet("search-config")]
        public IActionResult SearchConfig()
        {
            var googleApiKey = _config["Google:ApiKey"];
            var googleEngineId = _config["Google:SearchEngineId"];
            var searchProvider = _config["Search:Provider"];
            var searchGoogleCx = _config["Search:Google:Cx"];
            var searchGoogleKey = _config["Search:Google:ApiKey"];

            return Ok(new
            {
                configuration = new
                {
                    searchProvider = searchProvider ?? "(not set)",
                    googleApiKeyConfigured = !string.IsNullOrEmpty(googleApiKey) && googleApiKey != "SET_IN_USER_SECRETS",
                    googleApiKeyPrefix = googleApiKey?.Substring(0, Math.Min(10, googleApiKey?.Length ?? 0)) ?? "NULL",
                    googleEngineId = googleEngineId ?? "(not set)",
                    searchGoogleCxConfigured = !string.IsNullOrEmpty(searchGoogleCx) && searchGoogleCx != "SET_IN_USER_SECRETS",
                    searchGoogleCx = searchGoogleCx ?? "(not set)",
                    searchGoogleKeyConfigured = !string.IsNullOrEmpty(searchGoogleKey) && searchGoogleKey != "SET_IN_USER_SECRETS",
                },
                status = new
                {
                    ready = (!string.IsNullOrEmpty(googleApiKey) && googleApiKey != "SET_IN_USER_SECRETS") ||
                            (!string.IsNullOrEmpty(searchGoogleKey) && searchGoogleKey != "SET_IN_USER_SECRETS"),
                    message = "Check if API keys are properly configured"
                }
            });
        }

        [HttpGet("test-search")]
        public async Task<IActionResult> TestSearch([FromQuery] string query = "test")
        {
            try
            {
                var result = await _searchService.SearchAsync(query);
                
                return Ok(new
                {
                    success = result.Results?.Any() == true,
                    query = result.Query,
                    resultCount = result.Results?.Count ?? 0,
                    error = result.Error,
                    results = result.Results?.Take(3).Select(r => new
                    {
                        r.Title,
                        r.Url,
                        snippetPreview = r.Snippet?.Substring(0, Math.Min(100, r.Snippet?.Length ?? 0))
                    })
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }
    }

    public class TestMessageRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
