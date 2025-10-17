using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI.Agent.Application.Tools;

public sealed class BrowserExtractTool : ITool
{
    public string Name => "Browser.Extract";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var selector = input.TryGetProperty("selector", out var s) ? s.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
        {
            if (ctx.TryGetValue("nav:url", out var nav) && nav is string navUrl && !string.IsNullOrWhiteSpace(navUrl))
            {
                url = navUrl;
            }
            else
            {
                return (new { ok = false, error = "No URL available; provide input.url or run Browser.Goto first." }, new List<Artifact>(), "No URL available for extraction");
            }
        }
    // Prefer content containers; do NOT include body by default to avoid nav/boilerplate
    selector ??= "main, article, #content, [role=main]";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        string title = string.Empty;
        string text = string.Empty;

        // Launch Playwright Chromium headless
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36"
        });
        var page = await context.NewPageAsync();
    await page.GotoAsync(url!, new PageGotoOptions { Timeout = 30_000, WaitUntil = WaitUntilState.DOMContentLoaded });

        // CAPTCHA detection
        var captcha = await page.QuerySelectorAsync("iframe[title='reCAPTCHA'], iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]");
        if (captcha is not null)
        {
            var narr = new List<string> { $"🛑 CAPTCHA encountered at {url}; switch to API or request human help" };
            var capPayload = new { url, captcha = true, narration = narr };
            return (capPayload, new List<Artifact>(), string.Join(" ", narr));
        }

    // Wait up to 20s for a content selector
        var parts = selector.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IElementHandle? handle = null;
        var deadline = DateTime.UtcNow.AddSeconds(20);
        foreach (var sel in parts)
        {
            try
            {
                handle = await page.QuerySelectorAsync(sel);
                if (handle is not null) { break; }
            }
            catch { /* ignore */ }
            if (DateTime.UtcNow > deadline && handle is null) break;
        }

        if (handle is null)
        {
            // fallback to body only as a last resort
            handle = await page.QuerySelectorAsync("body");
        }

        title = await page.TitleAsync();
        var raw = handle is not null
            ? await handle.InnerTextAsync()
            : await page.InnerTextAsync("body");

        // normalize whitespace
        text = System.Text.RegularExpressions.Regex.Replace(raw ?? string.Empty, @"\s+", " ").Trim();
        var length = text.Length;
        bool looks404 = text.IndexOf("404", StringComparison.OrdinalIgnoreCase) >= 0
                        || text.IndexOf("page not found", StringComparison.OrdinalIgnoreCase) >= 0
                        || text.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
        if (length < 800 || looks404)
        {
            throw new InvalidOperationException("thin or invalid content");
        }

        var site = new Uri(url!).Host;
        var payload = new { url, title, text, site, length };
        return (payload, new List<Artifact>(), $"Extracted {length} chars from {site}");
    }
}
