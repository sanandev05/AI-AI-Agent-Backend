using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Contract.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Services
{
    public class GoogleSearchService : IGoogleSearchService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string? _searchEngineId;
        private readonly ILogger<GoogleSearchService> _logger;

        public GoogleSearchService(HttpClient httpClient, IConfiguration configuration, ILogger<GoogleSearchService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Google:ApiKey"];
            _searchEngineId = configuration["Google:SearchEngineId"];
            _logger = logger;

            _logger.LogInformation("GoogleSearchService created. ApiKey loaded: {ApiKeyLoaded}, SearchEngineId loaded: {SearchEngineIdLoaded}", 
                !string.IsNullOrEmpty(_apiKey), 
                !string.IsNullOrEmpty(_searchEngineId));
        }

        public async Task<WebSearchResultDto> SearchAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || string.IsNullOrWhiteSpace(_searchEngineId))
            {
                _logger.LogError("Google Search API key or Search Engine ID is not configured.");
                return new WebSearchResultDto { Error = "Search service is not configured." };
            }

            var requestUri = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}";

            try
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Google API Response: {Content}", content);

                var results = JsonSerializer.Deserialize<GoogleSearchResponse>(content);

                return new WebSearchResultDto
                {
                    Query = query,
                    Results = results.Items? // Add null-conditional operator
                        .Select(item => new WebSearchResult
                        {
                            Title = item.Title,
                            Url = item.Link,
                            Snippet = item.Snippet
                        }).ToList() ?? new List<WebSearchResult>() // If null, return empty list
                };
            }
            catch (HttpRequestException e)
            {
                _logger.LogError(e, "Error performing Google search for query: {Query}", query);
                return new WebSearchResultDto { Error = $"Error performing web search: {e.Message}" };
            }
            catch (JsonException e)
            {
                _logger.LogError(e, "Error deserializing Google search response for query: {Query}", query);
                return new WebSearchResultDto { Error = "Error parsing search results." };
            }
        }
    }

    // Helper classes for deserializing the Google Custom Search API response
    public class GoogleSearchResponse
    {
        public List<GoogleSearchItem>? Items { get; set; }
    }

    public class GoogleSearchItem
    {
        public string? Title { get; set; }
        public string? Link { get; set; }
        public string? Snippet { get; set; }
    }
}
