using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using AI_AI_Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI_AI_Agent.Application.Tools;

public sealed class BrowserExtractTool : ITool
{
    public string Name => "Browser.Extract";

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
        var selector = input.TryGetProperty("selector", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String ? (s.GetString() ?? "main, article, #content, [role=main]") : "main, article, #content, [role=main]";
        var timeoutSec = input.TryGetProperty("timeoutSec", out var to) && to.ValueKind == System.Text.Json.JsonValueKind.Number ? to.GetInt32() : 30;
        if (string.IsNullOrWhiteSpace(url))
        {
            // Auto-pick the first search result URL from context if available
            if (ctx.TryGetValue("search:results", out var val) && val is IEnumerable<object> arr)
            {
                foreach (var item in arr)
                {
                    var prop = item.GetType().GetProperty("url");
                    var cand = prop?.GetValue(item)?.ToString();
                    if (!string.IsNullOrWhiteSpace(cand)) { url = cand; break; }
                }
            }
            if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("url is required");
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, Args = new[] { "--no-sandbox", "--disable-blink-features=AutomationControlled" } });
        var context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, ViewportSize = null, UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36" });
        await context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
        var page = await context.NewPageAsync();
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutSec * 1000 });
        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = Math.Min(10000, timeoutSec * 1000) }); } catch { }
        if (await IsCaptchaAsync(page))
        {
            // Emit as thin; executor repair will pick next URL. We could throw a special 'captcha_detected' if pause/resume is added.
            throw new InvalidOperationException("captcha_detected");
        }
        // Try to scroll to trigger lazy content
        try { for (int i = 0; i < 4; i++) { await page.Mouse.WheelAsync(0, 1200); await page.WaitForTimeoutAsync(300); } } catch { }

        // Expand selector set for common sites (e.g., Wikipedia)
        var candidateSelectors = new[]
        {
            selector,
            "main, article, #content, [role=main]",
            "#mw-content-text, .vector-body, .mw-parser-output",
            "article, .article-content, .post, .entry-content",
            "div#content, div[role='main']"
        };
        IElementHandle? handle = null;
        foreach (var sel in candidateSelectors)
        {
            handle = await page.QuerySelectorAsync(sel);
            if (handle != null) { selector = sel; break; }
        }
        if (handle is null)
        {
            // fallback to body only as last resort
            handle = await page.QuerySelectorAsync("body");
        }
    if (handle is null) throw new InvalidOperationException($"Selector not found: {selector}");
    var rawHtml = (await handle.InnerHTMLAsync()) ?? string.Empty;
        var title = (await page.TitleAsync()) ?? string.Empty;

        // Remove common boilerplate
        string Clean(string html)
        {
            html = Regex.Replace(html, @"<script[\s\S]*?</script>", string.Empty, RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[\s\S]*?</style>", string.Empty, RegexOptions.IgnoreCase);
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text = Regex.Replace(text, "\\s+", " ").Trim();
            return text;
        }

    var text = Clean(rawHtml);
    var looks404 = text.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("page not found", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
    var excerpt = text.Length > 500 ? text.Substring(0, 500) : text;
    // Relax threshold to reduce false negatives; Wikipedia pages easily exceed this
    var thin = text.Length < 500 || looks404;
        var site = new Uri(url).Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
        var payload = new { url, title, excerpt, text, published = string.Empty, site, thin };

    if (thin) throw new InvalidOperationException("thin or invalid content");
    return (payload, Array.Empty<Artifact>(), $"Extracted {text.Length} chars from {site} using selector '{selector}'");
    }
}
