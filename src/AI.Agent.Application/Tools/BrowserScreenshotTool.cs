using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI.Agent.Application.Tools;

public sealed class BrowserScreenshotTool : ITool
{
    public string Name => "Browser.Screenshot";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var selector = input.TryGetProperty("selector", out var s) ? s.GetString() : null;
        var fullPage = input.TryGetProperty("fullPage", out var f) && f.GetBoolean();
        var purpose = input.TryGetProperty("purpose", out var p) ? p.GetString() : "process documentation";
        var stepNumber = input.TryGetProperty("stepNumber", out var sn) ? sn.GetInt32() : 1;

        // Narration context
        var narration = new List<string>();

        // Determine URL from context if not provided directly
        if (string.IsNullOrWhiteSpace(url))
        {
            if (ctx.TryGetValue("nav:url", out var nav) && nav is string navUrl && !string.IsNullOrWhiteSpace(navUrl))
            {
                url = navUrl;
            }
            else
            {
                return (new { ok = false, error = "No URL available; provide input.url or run Browser.Goto first." }, new List<Artifact>(), "No URL available for screenshot");
            }
        }

        narration.Add($"📸 Capturing screenshot of {url}");
        narration.Add($"🎯 Purpose: {purpose}");
        narration.Add($"📊 Step {stepNumber}: Documenting process state");

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
            
            narration.Add($"🌐 Navigating to target page...");
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });

            // CAPTCHA detection
            var captcha = await page.QuerySelectorAsync("iframe[title='reCAPTCHA'], iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]");
            if (captcha is not null)
            {
                narration.Add("🛑 CAPTCHA encountered; switch to API or request human help");
                var capPayload = new { url = page.Url, originalUrl = url, selector, fullPage, purpose, stepNumber, captcha = true, narration };
                var capSummary = string.Join(" ", narration);
                return (capPayload, new List<Artifact>(), capSummary);
            }
            
            narration.Add($"⏳ Waiting for page to stabilize...");
            await Task.Delay(2000, ct); // Allow dynamic content to load

            var screenshot = selector != null
                ? await page.Locator(selector).ScreenshotAsync(new LocatorScreenshotOptions { Type = ScreenshotType.Png })
                : await page.ScreenshotAsync(new PageScreenshotOptions 
                { 
                    Type = ScreenshotType.Png, 
                    FullPage = fullPage,
                    Quality = 90
                });

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var filename = $"screenshot_{stepNumber}_{timestamp}.png";
            var tmp = Path.Combine(Path.GetTempPath(), filename);
            await File.WriteAllBytesAsync(tmp, screenshot, ct);

            narration.Add($"✅ Screenshot captured successfully");
            narration.Add($"📁 File: {filename} ({screenshot.Length:N0} bytes)");
            narration.Add($"🔍 Content: {(selector != null ? $"Focused on '{selector}'" : (fullPage ? "Full page capture" : "Viewport capture"))}");

            // Get page metadata for context
            var title = await page.TitleAsync();
            var currentUrl = page.Url;
            
            narration.Add($"📄 Page title: {title}");
            if (currentUrl != url)
            {
                narration.Add($"🔄 Final URL: {currentUrl} (redirected from {url})");
            }

            var artifact = new Artifact(filename, tmp, "image/png", screenshot.Length);
            
            var payload = new
            {
                url = currentUrl,
                originalUrl = url,
                title = title,
                selector = selector,
                fullPage = fullPage,
                purpose = purpose,
                stepNumber = stepNumber,
                screenshotSize = screenshot.Length,
                timestamp = timestamp,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (payload, new List<Artifact> { artifact }, summary);
        }
        catch (Exception ex)
        {
            narration.Add($"❌ Screenshot failed: {ex.Message}");
            
            // Create a fallback placeholder image
            var placeholderPng = Convert.FromBase64String(Placeholders.ErrorPng);
            var fallbackFilename = $"screenshot_error_{stepNumber}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
            var fallbackPath = Path.Combine(Path.GetTempPath(), fallbackFilename);
            await File.WriteAllBytesAsync(fallbackPath, placeholderPng, ct);
            
            var fallbackArtifact = new Artifact(fallbackFilename, fallbackPath, "image/png", placeholderPng.Length);
            
            var errorPayload = new
            {
                url = url,
                error = ex.Message,
                purpose = purpose,
                stepNumber = stepNumber,
                narration = narration,
                fallback = true
            };

            var errorSummary = string.Join(" ", narration);
            return (errorPayload, new List<Artifact> { fallbackArtifact }, errorSummary);
        }
    }

    private static class Placeholders
    {
        // Error placeholder - red X image
        public const string ErrorPng = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNgYAAAAAMAAWgmWQ0AAAAASUVORK5CYII=";
    }
}
