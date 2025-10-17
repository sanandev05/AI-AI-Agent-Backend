using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI.Agent.Application.Tools;

public sealed class BrowserGotoTool : ITool
{
    public string Name => "Browser.Goto";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var fromSearch = input.TryGetProperty("fromSearchStep", out var fss) && (fss.ValueKind switch { JsonValueKind.True => true, JsonValueKind.String => string.Equals(fss.GetString(), "true", StringComparison.OrdinalIgnoreCase), _ => false });
        var index = input.TryGetProperty("index", out var idx) ? idx.GetInt32() : 0;
        var waitFor = input.TryGetProperty("waitFor", out var w) ? w.GetString() : "networkidle";
        var timeout = input.TryGetProperty("timeout", out var t) ? t.GetInt32() : 30000;

        // Resolve URL from search results when requested
        if (string.IsNullOrWhiteSpace(url) && fromSearch)
        {
            if (ctx.TryGetValue("search:lastResults", out var r) && r is IEnumerable<object> list)
            {
                var arr = list.Cast<object>().ToList();
                if (index < 0 || index >= arr.Count) index = 0;
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(arr[index]);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    url = doc.RootElement.TryGetProperty("url", out var uel) ? uel.GetString() : null;
                }
                catch { /* ignore */ }
            }
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("Browser.Goto requires a 'url' or 'fromSearchStep:true' with available results");
        }

        var narration = new List<string>
        {
            $"🌐 Navigating to: {url}",
            $"⏳ Wait condition: {waitFor}",
            $"⏱️ Timeout: {timeout}ms"
        };

        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions 
            { 
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" }
            });

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
            });

            var page = await context.NewPageAsync();
            
            narration.Add("🚀 Browser launched, navigating...");
            
            var waitUntil = waitFor.ToLowerInvariant() switch
            {
                "load" => WaitUntilState.Load,
                "domcontentloaded" => WaitUntilState.DOMContentLoaded,
                "networkidle" => WaitUntilState.NetworkIdle,
                _ => WaitUntilState.NetworkIdle
            };

            var response = await page.GotoAsync(url, new PageGotoOptions 
            { 
                WaitUntil = waitUntil, 
                Timeout = timeout 
            });

            var finalUrl = page.Url;
            var title = await page.TitleAsync();
            var status = response?.Status ?? 0;

            narration.Add($"✅ Navigation completed");

            // CAPTCHA detection
            var captcha = await page.QuerySelectorAsync("iframe[title='reCAPTCHA'], iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]");
            if (captcha is not null)
            {
                narration.Add("🛑 CAPTCHA encountered; switch to API or request human help");
                var capPayload = new { url = page.Url, title, status, captcha = true, narration };
                var capSummary = string.Join(" ", narration);
                return (capPayload, new List<Artifact>(), capSummary);
            }
            narration.Add($"📄 Page title: {title}");
            narration.Add($"🔗 Final URL: {finalUrl}");
            narration.Add($"📊 Status: {status}");

            if (finalUrl != url)
            {
                narration.Add($"🔄 Redirected from original URL");
            }

            // Store page state in context for other tools to use
            ctx["browser:currentUrl"] = finalUrl;
            ctx["nav:url"] = finalUrl;
            ctx["browser:pageTitle"] = title;
            ctx["browser:lastStatus"] = status;

            var payload = new
            {
                url = finalUrl,
                originalUrl = url,
                title = title,
                status = status,
                redirected = finalUrl != url,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (payload, new List<Artifact>(), summary);
        }
        catch (Exception ex)
        {
            narration.Add($"❌ Navigation failed: {ex.Message}");
            
            var errorPayload = new
            {
                url = url,
                error = ex.Message,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (errorPayload, new List<Artifact>(), summary);
        }
    }
}