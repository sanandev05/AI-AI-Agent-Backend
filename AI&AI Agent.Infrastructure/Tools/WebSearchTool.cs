using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Contract.Services;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class WebSearchTool : ITool
{
    public string Name => "WebSearch";
    public string Description => "Search the web for current information and return comprehensive results. Automatically fetches FULL content from top pages (not just snippets). Use this for: news, current events, schedules, prices, facts, any real-time data. Returns detailed articles with full text.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new { query = new { type = "string", description = "Search query - be specific and include key terms" } },
        required = new[] { "query" }
    };

    private readonly IGoogleSearchService _search;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IUrlSafetyService _urlSafety;
    private readonly ILogger<WebSearchTool> _logger;

    public WebSearchTool(IGoogleSearchService search, IHttpClientFactory httpClientFactory, IUrlSafetyService urlSafety, ILogger<WebSearchTool> logger)
    {
        _search = search;
        _httpClientFactory = httpClientFactory;
        _urlSafety = urlSafety;
        _logger = logger;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var q = args.GetProperty("query").GetString() ?? string.Empty;
        _logger.LogInformation("🔍 WebSearch with content extraction: {Query}", q);
        
        var sr = await _search.SearchAsync(q);
        var searchResults = sr.Results ?? new System.Collections.Generic.List<AI_AI_Agent.Contract.DTOs.WebSearchResult>();
        
        if (searchResults.Count == 0)
        {
            return new { query = q, results = new List<object>(), error = sr.Error ?? "No results found" };
        }
        
        // Take top 3 results and fetch their full content
        var topResults = searchResults.Take(3).ToList();
        var enhancedResults = new List<object>();
        
        foreach (var result in topResults)
        {
            try
            {
                // Skip if URL is null or empty
                if (string.IsNullOrWhiteSpace(result.Url))
                {
                    continue;
                }
                
                // Check URL safety
                if (!_urlSafety.IsAllowed(result.Url))
                {
                    _logger.LogWarning("⚠️ URL blocked by safety policy: {Url}", result.Url);
                    enhancedResults.Add(new
                    {
                        title = result.Title,
                        url = result.Url,
                        snippet = result.Snippet,
                        content = "[URL blocked by safety policy]"
                    });
                    continue;
                }
                
                // Fetch full content from the page
                var fullContent = await ExtractPageContentAsync(result.Url, ct);
                
                enhancedResults.Add(new
                {
                    title = result.Title,
                    url = result.Url,
                    snippet = result.Snippet,
                    content = fullContent // FULL article content!
                });
                
                _logger.LogInformation("✅ Extracted {Length} chars from {Url}", fullContent.Length, result.Url);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to extract content from {Url}", result.Url);
                
                // Fall back to snippet if content extraction fails
                enhancedResults.Add(new
                {
                    title = result.Title,
                    url = result.Url,
                    snippet = result.Snippet,
                    content = result.Snippet // Fallback to snippet
                });
            }
        }
        
        _logger.LogInformation("🎉 WebSearch complete with {Count} enhanced results", enhancedResults.Count);
        
        // Create a summary message to guide the AI
        var summaryText = new StringBuilder();
        summaryText.AppendLine($"Found {enhancedResults.Count} detailed results for: \"{q}\"");
        summaryText.AppendLine();
        
        for (int i = 0; i < enhancedResults.Count; i++)
        {
            var r = enhancedResults[i];
            var rDict = r as dynamic;
            summaryText.AppendLine($"[{i + 1}] {rDict.title}");
            summaryText.AppendLine($"    URL: {rDict.url}");
            summaryText.AppendLine($"    Content: {(rDict.content.Length > 150 ? rDict.content.Substring(0, 150) + "..." : rDict.content)}");
            summaryText.AppendLine();
        }
        
        summaryText.AppendLine("📌 INSTRUCTIONS:");
        summaryText.AppendLine("- Review the full content from each source above");
        summaryText.AppendLine("- Extract the SPECIFIC answer the user wants");
        summaryText.AppendLine("- Provide a DIRECT answer immediately (don't say 'I found...')");
        summaryText.AppendLine("- Include relevant facts, numbers, dates, names");
        summaryText.AppendLine("- Cite sources: [1], [2], [3]");
        
        return new { 
            query = q, 
            results = enhancedResults, 
            totalFound = searchResults.Count,
            summary = summaryText.ToString()
        };
    }
    
    private async Task<string> ExtractPageContentAsync(string url, CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
        
        var html = await httpClient.GetStringAsync(url, ct);
        
        // Parse HTML and extract main content
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);
        
        // Remove script, style, nav, footer, ads
        var elementsToRemove = doc.QuerySelectorAll("script, style, nav, footer, aside, .advertisement, .ad, .sidebar");
        foreach (var element in elementsToRemove)
        {
            element.Remove();
        }
        
        // Try to find main content area (common patterns)
        var mainContent = doc.QuerySelector("article") 
                       ?? doc.QuerySelector("main") 
                       ?? doc.QuerySelector("[role='main']")
                       ?? doc.QuerySelector(".content")
                       ?? doc.QuerySelector("#content")
                       ?? doc.QuerySelector("body");
        
        var text = mainContent?.TextContent ?? "";
        
        // Clean up whitespace
        var lines = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
        
        var cleanedText = string.Join("\n", lines);
        
        // Limit to ~3000 chars to avoid overwhelming the context
        if (cleanedText.Length > 3000)
        {
            cleanedText = cleanedText.Substring(0, 3000) + "... (content truncated)";
        }
        
        return cleanedText;
    }
}
