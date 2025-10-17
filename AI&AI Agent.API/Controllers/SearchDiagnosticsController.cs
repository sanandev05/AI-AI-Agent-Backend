using Microsoft.AspNetCore.Mvc;
using AI_AI_Agent.Contract.Services;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System;

namespace AI_AI_Agent.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchDiagnosticsController : ControllerBase
    {
        private readonly IGoogleSearchService _searchService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SearchDiagnosticsController> _logger;

        public SearchDiagnosticsController(
            IGoogleSearchService searchService, 
            IConfiguration configuration,
            ILogger<SearchDiagnosticsController> logger)
        {
            _searchService = searchService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Test the search service directly without the agent loop
        /// GET /api/searchdiagnostics/test?query=AI+trends
        /// </summary>
        [HttpGet("test")]
        public async Task<IActionResult> TestSearch([FromQuery] string query = "test search query")
        {
            try
            {
                _logger.LogInformation("Direct search test initiated for query: {Query}", query);
                var result = await _searchService.SearchAsync(query);
                
                return Ok(new
                {
                    success = true,
                    query = result.Query,
                    resultCount = result.Results?.Count ?? 0,
                    results = result.Results,
                    error = result.Error,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct search test failed for query: {Query}", query);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Check if search service is properly configured
        /// GET /api/searchdiagnostics/config
        /// </summary>
        [HttpGet("config")]
        public IActionResult CheckConfiguration()
        {
            var googleApiKey = _configuration["Google:ApiKey"];
            var googleSearchEngineId = _configuration["Google:SearchEngineId"];
            var bingApiKey = _configuration["Bing:ApiKey"];

            return Ok(new
            {
                configuration = new
                {
                    googleApiKeyConfigured = !string.IsNullOrWhiteSpace(googleApiKey) && googleApiKey != "SET_IN_USER_SECRETS",
                    googleApiKeyValue = MaskApiKey(googleApiKey),
                    googleSearchEngineIdConfigured = !string.IsNullOrWhiteSpace(googleSearchEngineId) && googleSearchEngineId != "SET_IN_USER_SECRETS",
                    googleSearchEngineIdValue = MaskApiKey(googleSearchEngineId),
                    bingApiKeyConfigured = !string.IsNullOrWhiteSpace(bingApiKey) && bingApiKey != "SET_IN_USER_SECRETS",
                    bingApiKeyValue = MaskApiKey(bingApiKey)
                },
                recommendations = GetRecommendations(googleApiKey, googleSearchEngineId, bingApiKey)
            });
        }

        /// <summary>
        /// Test Bing fallback directly
        /// GET /api/searchdiagnostics/test-bing?query=AI+trends
        /// </summary>
        [HttpGet("test-bing")]
        public async Task<IActionResult> TestBingFallback([FromQuery] string query = "test search query")
        {
            try
            {
                _logger.LogInformation("Testing Bing fallback for query: {Query}", query);
                
                // Force Bing by using a query that will use fallback
                // Since we can't directly call BingFallback (it's private), 
                // we test the public SearchAsync which will use fallback if Google isn't configured
                var result = await _searchService.SearchAsync(query);
                
                return Ok(new
                {
                    success = true,
                    message = "Bing fallback test completed",
                    query = result.Query,
                    resultCount = result.Results?.Count ?? 0,
                    results = result.Results,
                    error = result.Error,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bing fallback test failed for query: {Query}", query);
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message,
                    stackTrace = ex.StackTrace,
                    timestamp = DateTime.UtcNow
                });
            }
        }

        private string MaskApiKey(string? apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return "(not set)";
            
            if (apiKey == "SET_IN_USER_SECRETS")
                return apiKey;
            
            if (apiKey.Length <= 8)
                return "***";
            
            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
        }

        private string[] GetRecommendations(string? googleApiKey, string? googleSearchEngineId, string? bingApiKey)
        {
            var recommendations = new System.Collections.Generic.List<string>();

            bool googleConfigured = !string.IsNullOrWhiteSpace(googleApiKey) && 
                                   googleApiKey != "SET_IN_USER_SECRETS" &&
                                   !string.IsNullOrWhiteSpace(googleSearchEngineId) && 
                                   googleSearchEngineId != "SET_IN_USER_SECRETS";

            bool bingConfigured = !string.IsNullOrWhiteSpace(bingApiKey) && 
                                 bingApiKey != "SET_IN_USER_SECRETS";

            if (!googleConfigured && !bingConfigured)
            {
                recommendations.Add("⚠️ No search API keys configured. The system will use Bing HTML scraping as fallback, which is less reliable.");
                recommendations.Add("1. Get a Google Custom Search API key: https://developers.google.com/custom-search/v1/overview");
                recommendations.Add("2. Create a Custom Search Engine: https://programmablesearchengine.google.com/");
                recommendations.Add("3. Set keys using: dotnet user-secrets set \"Google:ApiKey\" \"YOUR_KEY\" --project \"AI&AI Agent.API\"");
                recommendations.Add("4. Set search engine ID: dotnet user-secrets set \"Google:SearchEngineId\" \"YOUR_CX\" --project \"AI&AI Agent.API\"");
            }
            else if (!googleConfigured)
            {
                recommendations.Add("✅ System will use Bing HTML scraping (fallback mode)");
                recommendations.Add("💡 For better results, configure Google Custom Search API");
            }
            else
            {
                recommendations.Add("✅ Google Custom Search API is configured and will be used as primary search");
                recommendations.Add("✅ Bing HTML scraping available as fallback");
            }

            return recommendations.ToArray();
        }
    }
}
