using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;

namespace AI_AI_Agent.Infrastructure.Tools;

public class WebBrowseTool : ITool
{
    public string Name => "WebBrowse";
    public string Description => "Navigates to a URL like a user (scrolls), can optionally click/type via selectors, and extracts visible text. Includes anti-bot/stealth and raw HTTP fallback.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            url = new
            {
                type = "string",
                description = "The fully qualified URL OR a search phrase (will trigger a Bing search)."
            },
            click = new { type = "string", description = "Optional CSS selector to click after navigating." },
            typeSelector = new { type = "string", description = "Optional CSS selector to type into." },
            typeText = new { type = "string", description = "Text to type if typeSelector is provided." },
            waitSelector = new { type = "string", description = "Optional CSS selector to wait for before extracting." },
            actions = new
            {
                type = "array",
                description = "Optional sequence of actions to perform (waitForSelector, type, click, press, waitForTimeout, submit). Each action is an object with { kind, selector, text, key, timeoutMs }.",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        kind = new { type = "string" },
                        selector = new { type = "string" },
                        text = new { type = "string" },
                        key = new { type = "string" },
                        timeoutMs = new { type = "number" }
                    }
                }
            },
            maxWaitMs = new { type = "number", description = "Maximum milliseconds to wait for dynamic content (default 8000)." },
            fallback = new { type = "boolean", description = "If true, attempt raw HTTP fetch & strip HTML when page blocked (default true)." }
        },
        required = new[] { "url" }
    };

    private readonly IBrowser _browser;
    private readonly AI_AI_Agent.Application.Services.IUrlSafetyService _urlSafety;

    public WebBrowseTool(IBrowser browser, AI_AI_Agent.Application.Services.IUrlSafetyService urlSafety)
    {
        _browser = browser;
        _urlSafety = urlSafety;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("url", out var urlProp) || urlProp.ValueKind != JsonValueKind.String)
        {
            return "Error: 'url' parameter is missing or not a string.";
        }

        var url = urlProp.GetString()!;
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            url = $"https://www.bing.com/search?q={System.Net.WebUtility.UrlEncode(url)}";
        }

        if (!_urlSafety.IsAllowed(url))
        {
            var reason = _urlSafety.GetViolationReason(url) ?? "URL is not allowed by policy.";
            return $"Blocked: {reason}";
        }

        // Extract optional args
    string? waitSelector = args.TryGetProperty("waitSelector", out var ws) && ws.ValueKind == JsonValueKind.String ? ws.GetString() : null;
    string? clickSelector = args.TryGetProperty("click", out var cs) && cs.ValueKind == JsonValueKind.String ? cs.GetString() : null;
    string? typeSelector = args.TryGetProperty("typeSelector", out var ts) && ts.ValueKind == JsonValueKind.String ? ts.GetString() : null;
    string? typeText = args.TryGetProperty("typeText", out var tt) && tt.ValueKind == JsonValueKind.String ? tt.GetString() : null;
        int maxWaitMs = args.TryGetProperty("maxWaitMs", out var mw) && mw.ValueKind == JsonValueKind.Number ? mw.GetInt32() : 8000;
        bool fallback = !(args.TryGetProperty("fallback", out var fb) && fb.ValueKind == JsonValueKind.False);

        // Create a fresh context so we can spoof user agent & apply stealth JS
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36",
            Locale = "en-US",
            TimezoneId = "UTC",
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
        });
        var page = await context.NewPageAsync();
        try
        {
            // Basic stealth: remove navigator.webdriver, add plugins & languages before any navigation
            await page.AddInitScriptAsync(@"() => { Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                window.chrome = { runtime: {} }; 
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US','en'] });
                Object.defineProperty(navigator, 'platform', { get: () => 'Win32' });
            }");

            await page.GotoAsync(url, new PageGotoOptions { Timeout = 30000, WaitUntil = WaitUntilState.NetworkIdle });

            // Optional wait for selector
            if (!string.IsNullOrWhiteSpace(waitSelector))
            {
                try { await page.WaitForSelectorAsync(waitSelector, new PageWaitForSelectorOptions { Timeout = maxWaitMs }); } catch { /* ignore */ }
            }

            // Optional type into input
            if (!string.IsNullOrWhiteSpace(typeSelector) && typeText is not null)
            {
                try { await page.FillAsync(typeSelector, typeText, new() { Timeout = maxWaitMs }); }
                catch { /* ignore typing errors */ }
            }

            // Optional click
            if (!string.IsNullOrWhiteSpace(clickSelector))
            {
                try { await page.ClickAsync(clickSelector, new() { Timeout = maxWaitMs }); }
                catch { /* ignore click errors */ }
            }

            // Action sequence
            if (args.TryGetProperty("actions", out var actionsProp) && actionsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var act in actionsProp.EnumerateArray())
                {
                    if (ct.IsCancellationRequested) break;
                    if (act.ValueKind != JsonValueKind.Object) continue;
                    var kindStr = act.TryGetProperty("kind", out var kprop) && kprop.ValueKind == JsonValueKind.String ? kprop.GetString() ?? string.Empty : string.Empty;
                    var selectorStr = act.TryGetProperty("selector", out var sprop) && sprop.ValueKind == JsonValueKind.String ? sprop.GetString() : null;
                    var textStr = act.TryGetProperty("text", out var tprop) && tprop.ValueKind == JsonValueKind.String ? tprop.GetString() : null;
                    var keyStr = act.TryGetProperty("key", out var k2prop) && k2prop.ValueKind == JsonValueKind.String ? k2prop.GetString() : null;
                    var timeoutMs = act.TryGetProperty("timeoutMs", out var toprop) && toprop.ValueKind == JsonValueKind.Number ? toprop.GetInt32() : maxWaitMs;

                    var kind = (kindStr ?? string.Empty).ToLowerInvariant();
                    try
                    {
                        switch (kind)
                        {
                            case "waitforselector":
                                if (!string.IsNullOrWhiteSpace(selectorStr))
                                    await page.WaitForSelectorAsync(selectorStr, new() { Timeout = timeoutMs });
                                break;
                            case "type":
                                if (!string.IsNullOrWhiteSpace(selectorStr) && textStr is not null)
                                    await page.FillAsync(selectorStr, textStr, new() { Timeout = timeoutMs });
                                break;
                            case "click":
                                if (!string.IsNullOrWhiteSpace(selectorStr))
                                    await page.ClickAsync(selectorStr, new() { Timeout = timeoutMs });
                                break;
                            case "press":
                                if (!string.IsNullOrWhiteSpace(selectorStr) && !string.IsNullOrWhiteSpace(keyStr))
                                    await page.PressAsync(selectorStr, keyStr, new() { Timeout = timeoutMs });
                                break;
                            case "waitfortimeout":
                                await page.WaitForTimeoutAsync((float)Math.Max(0, timeoutMs));
                                break;
                            case "submit":
                                if (!string.IsNullOrWhiteSpace(selectorStr))
                                {
                                    // Try to submit a form by pressing Enter on an input or clicking a submit button
                                    try { await page.PressAsync(selectorStr, "Enter", new() { Timeout = timeoutMs }); }
                                    catch { try { await page.ClickAsync(selectorStr + " [type=submit]", new() { Timeout = timeoutMs }); } catch { } }
                                }
                                break;
                        }
                    }
                    catch { /* ignore individual action failures */ }
                }
            }

            // Scroll to trigger lazy load
            var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
            while (DateTime.UtcNow < deadline)
            {
                await page.EvaluateAsync("() => window.scrollBy(0, document.body.scrollHeight)");
                await Task.Delay(350, ct);
            }

            // Detect common bot/verification pages
            var html = await page.ContentAsync();
            if (IsBotChallenge(html))
            {
                if (fallback)
                {
                    var httpResult = await TryRawHttpAsync(url, ct);
                    if (httpResult is not null)
                        return httpResult;
                }
                return "Encountered bot/verification challenge; couldn’t extract content.";
            }

            var body = await page.QuerySelectorAsync("body");
            if (body is null) return "Could not find body content.";
            await body.EvaluateAsync("element => { const scripts = element.querySelectorAll('script, style, noscript'); scripts.forEach(s => s.remove()); }");
            var textContent = (await body.InnerTextAsync())?.Trim();
            if (string.IsNullOrWhiteSpace(textContent))
            {
                // Fallback to raw HTTP if nothing extracted and allowed
                if (fallback)
                {
                    var httpResult = await TryRawHttpAsync(url, ct);
                    if (httpResult is not null)
                        return httpResult;
                }
                return "No readable text extracted.";
            }
            return textContent;
        }
        catch (Exception ex)
        {
            if (fallback)
            {
                var httpResult = await TryRawHttpAsync(url, ct);
                if (httpResult is not null)
                    return httpResult + $"\n(Note: Browser error: {ex.Message})";
            }
            return $"Error browsing to URL: {ex.Message}";
        }
        finally
        {
            try { await page.CloseAsync(); } catch { }
            try { await context.CloseAsync(); } catch { }
        }
    }

    private static bool IsBotChallenge(string html)
    {
        if (string.IsNullOrEmpty(html)) return false;
        var lowered = html.ToLowerInvariant();
        return lowered.Contains("captcha") ||
               lowered.Contains("are you a robot") ||
               lowered.Contains("checking your browser") ||
               lowered.Contains("cloudflare") ||
               lowered.Contains("attention required");
    }

    private static async Task<string?> TryRawHttpAsync(string url, CancellationToken ct)
    {
        try
        {
            // Only attempt if url is http/https
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) return null;
            using var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip | DecompressionMethods.Brotli };
            using var client = new HttpClient(handler);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            var html = await client.GetStringAsync(url, ct);
            if (IsBotChallenge(html)) return null; // Still blocked
            return StripHtml(html);
        }
        catch { return null; }
    }

    private static string StripHtml(string html)
    {
    html = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
    html = Regex.Replace(html, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
