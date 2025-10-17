using System.Text.Json;
using System.Text.RegularExpressions;
using AI_AI_Agent.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace AI_AI_Agent.Application.Tools;

public sealed class WebResearchAgentTool : ITool
{
    public string Name => "WebResearchAgent";
    
    private readonly SearchApiTool _searchTool;
    private readonly WebContentFetchTool _fetchTool;
    private readonly SummarizeTool _summarizeTool;
    private readonly ILogger<WebResearchAgentTool> _logger;

    public WebResearchAgentTool(SearchApiTool searchTool, WebContentFetchTool fetchTool, SummarizeTool summarizeTool, ILogger<WebResearchAgentTool> logger)
    {
        _searchTool = searchTool;
        _fetchTool = fetchTool;
        _summarizeTool = summarizeTool;
        _logger = logger;
    }

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var query = input.TryGetProperty("query", out var q) ? q.GetString() : null;
        var urls = ExtractUrls(input);
        var userPrompt = input.TryGetProperty("userPrompt", out var up) ? up.GetString() : null;
        var summaryMode = input.TryGetProperty("summaryMode", out var sm) && sm.ValueKind == JsonValueKind.String 
            ? sm.GetString() : "smart";

        if (string.IsNullOrWhiteSpace(query) && urls.Count == 0 && string.IsNullOrWhiteSpace(userPrompt))
        {
            throw new ArgumentException("Either query, urls, or userPrompt is required");
        }

        var allArtifacts = new List<Artifact>();
        var steps = new List<string>();
        
        try
        {
            // Step 1: If we have a query but no URLs, search for relevant URLs first
            if (!string.IsNullOrWhiteSpace(query) && urls.Count == 0)
            {
                _logger.LogInformation("Searching for URLs related to query: {Query}", query);
                
                var searchInput = JsonSerializer.SerializeToElement(new { query = query, maxResults = 5 });
                var searchResult = await _searchTool.RunAsync(searchInput, ctx, ct);
                
                if (searchResult.payload != null)
                {
                    steps.Add($"Search completed: Found search results for '{query}'");
                    allArtifacts.AddRange(searchResult.artifacts);
                    
                    // Extract URLs from search results
                    urls.AddRange(ExtractUrlsFromSearchResults(searchResult.payload));
                }
            }

            // Step 2: Fetch content from URLs
            if (urls.Count > 0)
            {
                _logger.LogInformation("Fetching content from {UrlCount} URLs", urls.Count);
                
                var fetchInput = JsonSerializer.SerializeToElement(new 
                { 
                    urls = urls.ToArray(),
                    method = "browser", // Use browser for better content extraction
                    timeoutSec = 30
                });
                
                var fetchResult = await _fetchTool.RunAsync(fetchInput, ctx, ct);
                
                if (fetchResult.payload != null)
                {
                    steps.Add($"Content fetched: Retrieved content from {urls.Count} URLs");
                    allArtifacts.AddRange(fetchResult.artifacts);
                }
            }

            // Step 3: Summarize the content based on user prompt or query
            _logger.LogInformation("Generating summary in mode: {SummaryMode}", summaryMode);
            
            var summarizeInput = new Dictionary<string, object>
            {
                ["mode"] = summaryMode ?? "smart",
                ["minWords"] = summaryMode == "smart" ? 100 : 50
            };

            // If we have a specific user prompt, include it for context-aware summarization
            if (!string.IsNullOrWhiteSpace(userPrompt))
            {
                summarizeInput["userContext"] = userPrompt;
            }
            else if (!string.IsNullOrWhiteSpace(query))
            {
                summarizeInput["userContext"] = $"User query: {query}";
            }

            var summarizeJsonInput = JsonSerializer.SerializeToElement(summarizeInput);
            var summarizeResult = await _summarizeTool.RunAsync(summarizeJsonInput, ctx, ct);

            steps.Add($"Summary generated: Created {summaryMode} summary of web content");
            if (summarizeResult.artifacts != null)
            {
                allArtifacts.AddRange(summarizeResult.artifacts);
            }

            // Prepare final payload
            var finalPayload = new
            {
                query = query,
                userPrompt = userPrompt,
                urlsProcessed = urls.Count,
                steps = steps.ToArray(),
                summary = summarizeResult.payload?.ToString() ?? "No summary generated",
                originalUrls = urls.ToArray(),
                mode = summaryMode
            };

            var finalSummary = $"Web research completed: Processed {urls.Count} URLs and generated summary";
            if (!string.IsNullOrWhiteSpace(query))
            {
                finalSummary += $" for query '{query}'";
            }

            return (finalPayload, allArtifacts, finalSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during web research agent execution");
            
            var errorPayload = new
            {
                error = ex.Message,
                query = query,
                userPrompt = userPrompt,
                urlsAttempted = urls.ToArray(),
                stepsCompleted = steps.ToArray()
            };
            
            return (errorPayload, allArtifacts, $"Web research failed: {ex.Message}");
        }
    }

    private static List<string> ExtractUrls(JsonElement input)
    {
        var urls = new List<string>();

        // Single URL
        if (input.TryGetProperty("url", out var singleUrl) && singleUrl.ValueKind == JsonValueKind.String)
        {
            var urlString = singleUrl.GetString();
            if (!string.IsNullOrWhiteSpace(urlString) && IsValidUrl(urlString))
            {
                urls.Add(urlString);
            }
        }

        // Multiple URLs
        if (input.TryGetProperty("urls", out var urlArray) && urlArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var urlElement in urlArray.EnumerateArray())
            {
                if (urlElement.ValueKind == JsonValueKind.String)
                {
                    var urlString = urlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(urlString) && IsValidUrl(urlString))
                    {
                        urls.Add(urlString);
                    }
                }
            }
        }

        // Extract URLs from text content
        if (input.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            var textContent = text.GetString() ?? "";
            urls.AddRange(ExtractUrlsFromText(textContent));
        }

        return urls.Distinct().ToList();
    }

    private static List<string> ExtractUrlsFromText(string text)
    {
        var urls = new List<string>();
        var urlPattern = @"https?://[^\s<>""{}|\\^`\[\]]+";
        var matches = Regex.Matches(text, urlPattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            if (IsValidUrl(match.Value))
            {
                urls.Add(match.Value);
            }
        }
        
        return urls;
    }

    private static List<string> ExtractUrlsFromSearchResults(object searchPayload)
    {
        var urls = new List<string>();
        
        try
        {
            var json = JsonSerializer.Serialize(searchPayload);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
            {
                foreach (var result in results.EnumerateArray())
                {
                    if (result.TryGetProperty("url", out var urlElement) && 
                        urlElement.ValueKind == JsonValueKind.String)
                    {
                        var url = urlElement.GetString();
                        if (!string.IsNullOrWhiteSpace(url) && IsValidUrl(url))
                        {
                            urls.Add(url);
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Fallback: try to find URL properties in the payload
            var payloadString = searchPayload.ToString() ?? "";
            urls.AddRange(ExtractUrlsFromText(payloadString));
        }
        
        return urls.Take(5).ToList(); // Limit to top 5 results
    }

    private static bool IsValidUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Scheme == "http" || uri.Scheme == "https";
        }
        catch
        {
            return false;
        }
    }
}