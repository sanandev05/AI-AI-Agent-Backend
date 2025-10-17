using System.Text.RegularExpressions;
using System.Text;
using System.Net.Http;
using AI_AI_Agent.Domain.Events;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Application.Tools;

public sealed class WebContentFetchTool : ITool
{
    public string Name => "WebContentFetch";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebContentFetchTool> _logger;

    public WebContentFetchTool(IHttpClientFactory httpClientFactory, ILogger<WebContentFetchTool> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static async Task<bool> IsCaptchaAsync(IPage page)
    {
        try
        {
            if (await page.Locator("iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]").CountAsync() > 0) return true;
            if (page.Url.Contains("captcha", StringComparison.OrdinalIgnoreCase)) return true;
            if (await page.GetByText(new Regex("verify you are human|are you a robot|captcha", RegexOptions.IgnoreCase)).CountAsync() > 0) return true;
        }
        catch { }
        return false;
    }

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var urls = new List<string>();
        
        // Check if this should use results from a search step
        var fromSearchStep = input.TryGetProperty("fromSearchStep", out var fss) && fss.GetBoolean();
        
        if (fromSearchStep)
        {
            _logger.LogInformation("Attempting to extract URLs from search results in context");
            
            // Extract URLs from search results in context
            if (ctx.TryGetValue("search:results", out var searchData))
            {
                _logger.LogInformation("Found search:results in context, type: {Type}", searchData?.GetType().Name ?? "null");
                
                if (searchData is IList<object> resultsList)
                {
                    _logger.LogInformation("Processing {Count} search results", resultsList.Count);
                    
                    foreach (var item in resultsList)
                    {
                        if (item != null)
                        {
                            // Get the url property using reflection (anonymous type from SearchApiTool)
                            var itemType = item.GetType();
                            var urlProperty = itemType.GetProperty("url");
                            
                            if (urlProperty != null)
                            {
                                var resultUrl = urlProperty.GetValue(item)?.ToString();
                                if (!string.IsNullOrWhiteSpace(resultUrl) && IsValidUrl(resultUrl))
                                {
                                    urls.Add(resultUrl);
                                    _logger.LogInformation("Extracted URL: {Url}", resultUrl);
                                }
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("search:results is not an IList<object>, actual type: {Type}", 
                                     searchData?.GetType().FullName ?? "null");
                }
            }
            
            // If no URLs found in search results, check for alternative sources
            if (urls.Count == 0)
            {
                _logger.LogWarning("No URLs found in search results, trying alternative extraction methods");
                
                // Look for URLs in any context data
                foreach (var kvp in ctx)
                {
                    _logger.LogDebug("Checking context key: {Key}", kvp.Key);
                    
                    if (kvp.Value != null)
                    {
                        var valueStr = kvp.Value.ToString() ?? "";
                        var extractedUrls = ExtractUrlsFromText(valueStr);
                        if (extractedUrls.Count > 0)
                        {
                            _logger.LogInformation("Extracted {Count} URLs from context key {Key}", extractedUrls.Count, kvp.Key);
                            urls.AddRange(extractedUrls);
                            if (urls.Count >= 3) break; // Limit to first few URLs
                        }
                    }
                }
            }
            
            _logger.LogInformation("Total URLs extracted from search results: {Count}", urls.Count);
        }

        // Handle single URL
        if (!string.IsNullOrWhiteSpace(url))
        {
            urls.Add(url);
        }

        // Handle multiple URLs
        if (input.TryGetProperty("urls", out var urlsArray) && urlsArray.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var urlElement in urlsArray.EnumerateArray())
            {
                if (urlElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var urlString = urlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(urlString))
                    {
                        urls.Add(urlString);
                    }
                }
            }
        }

        // Remove duplicates and limit count
        urls = urls.Distinct().Take(5).ToList();

        if (urls.Count == 0)
        {
            _logger.LogError("No URLs found in input or context. Available context keys: {Keys}", 
                string.Join(", ", ctx.Keys));
            
            // If fromSearchStep was true but we found no URLs, provide helpful error
            if (fromSearchStep)
            {
                var contextInfo = ctx.Keys.Any() ? 
                    $"Available context keys: {string.Join(", ", ctx.Keys)}" : 
                    "No context data available";
                
                throw new ArgumentException($"No URLs found in search results or context. {contextInfo}");
            }
            else
            {
                throw new ArgumentException("At least one URL is required. No URLs found in input.");
            }
        }

        var fetchMethod = input.TryGetProperty("method", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String 
            ? m.GetString()?.ToLowerInvariant() : "browser";
        
        var timeoutSec = input.TryGetProperty("timeoutSec", out var to) && to.ValueKind == System.Text.Json.JsonValueKind.Number 
            ? to.GetInt32() : 30;

        var results = new List<object>();
        var totalContentLength = 0;
        var successfulFetches = 0;

        foreach (var targetUrl in urls)
        {
            try
            {
                object? result = null;

                if (fetchMethod == "http")
                {
                    result = await FetchViaHttpAsync(targetUrl, timeoutSec, ct);
                }
                else
                {
                    result = await FetchViaBrowserAsync(targetUrl, timeoutSec, ct);
                }

                if (result != null)
                {
                    results.Add(result);
                    successfulFetches++;
                    
                    // Extract content length for summary
                    if (result.GetType().GetProperty("text")?.GetValue(result) is string text)
                    {
                        totalContentLength += text.Length;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch content from {Url}", targetUrl);
                
                // Add failed result with error info
                var errorResult = new
                {
                    url = targetUrl,
                    title = "Error",
                    text = $"Failed to fetch content: {ex.Message}",
                    excerpt = ex.Message.Length > 200 ? ex.Message.Substring(0, 200) + "..." : ex.Message,
                    site = GetSiteName(targetUrl),
                    error = true,
                    errorMessage = ex.Message
                };
                results.Add(errorResult);
            }
        }

        // Store results in context for other tools (like SummarizeTool) to use
        ctx["web_content:results"] = results;
        ctx["web_content:urls"] = urls;

        var payload = new 
        { 
            results = results,
            totalUrls = urls.Count,
            successfulFetches = successfulFetches,
            totalContentLength = totalContentLength
        };

        var summary = $"Fetched content from {successfulFetches}/{urls.Count} URLs, total {totalContentLength:N0} characters";
        
        return (payload, new List<Artifact>(), summary);
    }

    private async Task<object> FetchViaHttpAsync(string url, int timeoutSec, CancellationToken ct)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(timeoutSec);
        httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36");

        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);
        var title = ExtractTitle(html);
        var text = CleanHtmlToText(html);
        var excerpt = text.Length > 500 ? text.Substring(0, 500) + "..." : text;
        var site = GetSiteName(url);

        return new
        {
            url = url,
            title = title,
            text = text,
            excerpt = excerpt,
            site = site,
            method = "http",
            contentLength = text.Length
        };
    }

    private async Task<object> FetchViaBrowserAsync(string url, int timeoutSec, CancellationToken ct)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() 
        { 
            Headless = true, 
            Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" } 
        });
        
        var context = await browser.NewContextAsync(new() 
        { 
            IgnoreHTTPSErrors = true, 
            ViewportSize = null, 
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36" 
        });
        
        await context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
        var page = await context.NewPageAsync();
        
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutSec * 1000 });
        
        try 
        { 
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = Math.Min(10000, timeoutSec * 1000) }); 
        } 
        catch { }
        
        if (await IsCaptchaAsync(page))
        {
            throw new InvalidOperationException("CAPTCHA detected - cannot proceed with automated fetching");
        }

        // Try to scroll to trigger lazy content
        try 
        { 
            for (int i = 0; i < 4; i++) 
            { 
                await page.Mouse.WheelAsync(0, 1200); 
                await page.WaitForTimeoutAsync(300); 
            } 
        } 
        catch { }

        // Try various selectors to get main content
        var candidateSelectors = new[]
        {
            "main, article, #content, [role=main]",
            "#mw-content-text, .vector-body, .mw-parser-output", // Wikipedia
            "article, .article-content, .post, .entry-content",
            "div#content, div[role='main']",
            "body" // Fallback
        };

        IElementHandle? handle = null;
        string usedSelector = "body";
        
        foreach (var selector in candidateSelectors)
        {
            handle = await page.QuerySelectorAsync(selector);
            if (handle != null) 
            { 
                usedSelector = selector; 
                break; 
            }
        }

        if (handle == null)
        {
            throw new InvalidOperationException("Could not find any content on the page");
        }

        var rawHtml = (await handle.InnerHTMLAsync()) ?? string.Empty;
        var title = (await page.TitleAsync()) ?? string.Empty;
        var text = CleanHtmlToText(rawHtml);
        var excerpt = text.Length > 500 ? text.Substring(0, 500) + "..." : text;
        var site = GetSiteName(url);

        // Check if content is too thin or looks like an error page
        var looks404 = text.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("page not found", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;

        if (text.Length < 100 || looks404)
        {
            throw new InvalidOperationException("Page contains insufficient or invalid content");
        }

        return new
        {
            url = url,
            title = title,
            text = text,
            excerpt = excerpt,
            site = site,
            method = "browser",
            selector = usedSelector,
            contentLength = text.Length
        };
    }

    private static string CleanHtmlToText(string html)
    {
        // Remove scripts and styles
        html = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<nav[\s\S]*?</nav>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<footer[\s\S]*?</footer>", string.Empty, RegexOptions.IgnoreCase);
        html = Regex.Replace(html, @"<header[\s\S]*?</header>", string.Empty, RegexOptions.IgnoreCase);
        
        // Convert HTML to plain text
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        
        return text;
    }

    private static string ExtractTitle(string html)
    {
        var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]*)</title>", RegexOptions.IgnoreCase);
        return titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Untitled";
    }

    private static string GetSiteName(string url)
    {
        try
        {
            return new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return "Unknown Site";
        }
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