using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI.Agent.Application.Tools;

public sealed class BrowserScrollTool : ITool
{
    public string Name => "Browser.Scroll";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var direction = input.TryGetProperty("direction", out var d) ? d.GetString() : "down";
        var amount = input.TryGetProperty("amount", out var a) ? a.GetInt32() : 500;
        var selector = input.TryGetProperty("selector", out var s) ? s.GetString() : null;

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("url is required for Browser.Scroll");
        }

        var narration = new List<string>
        {
            $"📜 Scrolling {direction} on: {url}",
            $"📏 Scroll amount: {amount}px",
            selector != null ? $"🎯 Target element: {selector}" : "🎯 Target: page body"
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
            
            narration.Add("🌐 Navigating to page for scrolling...");
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });

            // CAPTCHA detection
            var captcha = await page.QuerySelectorAsync("iframe[title='reCAPTCHA'], iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]");
            if (captcha is not null)
            {
                narration.Add("🛑 CAPTCHA encountered; switch to API or request human help");
                var capPayload = new { url, direction, amount, selector, captcha = true, narration };
                return (capPayload, new List<Artifact>(), string.Join(" ", narration));
            }
            
            // Get initial scroll position
            var initialY = await page.EvaluateAsync<int>("window.pageYOffset");
            narration.Add($"📍 Initial scroll position: {initialY}px");

            // Perform scroll
            if (!string.IsNullOrWhiteSpace(selector))
            {
                // Scroll specific element
                var element = page.Locator(selector);
                await element.ScrollIntoViewIfNeededAsync();
                narration.Add($"🎯 Scrolled element '{selector}' into view");
            }
            else
            {
                // Scroll the page
                var scrollDirection = direction.ToLowerInvariant() switch
                {
                    "up" => -amount,
                    "down" => amount,
                    "top" => -999999,
                    "bottom" => 999999,
                    _ => amount
                };

                await page.EvaluateAsync($"window.scrollBy(0, {scrollDirection})");
                narration.Add($"📜 Scrolled page {direction} by {Math.Abs(scrollDirection)}px");
            }

            // Wait for any dynamic content to load
            await Task.Delay(1000, ct);

            // Get final scroll position
            var finalY = await page.EvaluateAsync<int>("window.pageYOffset");
            var scrollDelta = finalY - initialY;
            
            narration.Add($"📍 Final scroll position: {finalY}px");
            narration.Add($"📏 Actual scroll distance: {Math.Abs(scrollDelta)}px");

            // Get page info after scroll
            var title = await page.TitleAsync();
            var currentUrl = page.Url;

            var payload = new
            {
                url = currentUrl,
                direction = direction,
                requestedAmount = amount,
                actualScroll = scrollDelta,
                initialPosition = initialY,
                finalPosition = finalY,
                title = title,
                selector = selector,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (payload, new List<Artifact>(), summary);
        }
        catch (Exception ex)
        {
            narration.Add($"❌ Scrolling failed: {ex.Message}");
            
            var errorPayload = new
            {
                url = url,
                direction = direction,
                amount = amount,
                selector = selector,
                error = ex.Message,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (errorPayload, new List<Artifact>(), summary);
        }
    }
}