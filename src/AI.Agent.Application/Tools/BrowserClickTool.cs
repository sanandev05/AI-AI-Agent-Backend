using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI.Agent.Application.Tools;

public sealed class BrowserClickTool : ITool
{
    public string Name => "Browser.Click";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        var selector = input.TryGetProperty("selector", out var s) ? s.GetString() : null;
        var text = input.TryGetProperty("text", out var t) ? t.GetString() : null;
        var waitForNavigation = input.TryGetProperty("waitForNavigation", out var wfn) && wfn.GetBoolean();
        var timeout = input.TryGetProperty("timeout", out var to) ? to.GetInt32() : 30000;

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("url is required for Browser.Click");
        }

        if (string.IsNullOrWhiteSpace(selector) && string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Either 'selector' or 'text' must be provided for Browser.Click");
        }

        var narration = new List<string>
        {
            $"🖱️ Clicking element on: {url}",
            selector != null ? $"🎯 Selector: {selector}" : $"📝 Text: {text}",
            waitForNavigation ? "⏳ Waiting for navigation after click" : "⏳ No navigation wait"
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
            
            narration.Add("🌐 Navigating to page for clicking...");
            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = timeout });

            // CAPTCHA detection before interaction
            var captcha = await page.QuerySelectorAsync("iframe[title='reCAPTCHA'], iframe[src*='recaptcha' i], iframe[src*='hcaptcha' i]");
            if (captcha is not null)
            {
                narration.Add("🛑 CAPTCHA encountered; switch to API or request human help");
                var capPayload = new { url, selector, text, captcha = true, narration };
                return (capPayload, new List<Artifact>(), string.Join(" ", narration));
            }
            
            var initialUrl = page.Url;
            var initialTitle = await page.TitleAsync();

            // Find and click the element
            ILocator element;
            if (!string.IsNullOrWhiteSpace(selector))
            {
                element = page.Locator(selector);
                narration.Add($"🔍 Located element by selector: {selector}");
            }
            else
            {
                // Find by text content
                element = page.GetByText(text!);
                narration.Add($"🔍 Located element by text: {text}");
            }

            // Wait for element to be visible and clickable
            await element.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = timeout });
            narration.Add("👁️ Element is visible and ready");

            // Scroll element into view if needed
            await element.ScrollIntoViewIfNeededAsync();

            // Perform the click
            if (waitForNavigation)
            {
                narration.Add("🖱️ Clicking and waiting for navigation...");
                await page.RunAndWaitForNavigationAsync(async () =>
                {
                    await element.ClickAsync(new LocatorClickOptions { Timeout = timeout });
                }, new PageRunAndWaitForNavigationOptions { Timeout = timeout });
            }
            else
            {
                narration.Add("🖱️ Clicking element...");
                await element.ClickAsync(new LocatorClickOptions { Timeout = timeout });
                await Task.Delay(1000, ct); // Brief wait for any dynamic changes
            }

            var finalUrl = page.Url;
            var finalTitle = await page.TitleAsync();
            var navigationOccurred = finalUrl != initialUrl;

            narration.Add("✅ Click completed successfully");
            if (navigationOccurred)
            {
                narration.Add($"🔄 Navigation occurred: {initialUrl} → {finalUrl}");
                narration.Add($"📄 Title changed: {initialTitle} → {finalTitle}");
            }
            else
            {
                narration.Add("📍 No navigation, stayed on same page");
            }

            // Update context with new state
            ctx["browser:currentUrl"] = finalUrl;
            ctx["browser:pageTitle"] = finalTitle;

            var payload = new
            {
                url = finalUrl,
                originalUrl = url,
                initialUrl = initialUrl,
                title = finalTitle,
                selector = selector,
                text = text,
                navigationOccurred = navigationOccurred,
                waitedForNavigation = waitForNavigation,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (payload, new List<Artifact>(), summary);
        }
        catch (Exception ex)
        {
            narration.Add($"❌ Click failed: {ex.Message}");
            
            var errorPayload = new
            {
                url = url,
                selector = selector,
                text = text,
                error = ex.Message,
                timestamp = DateTime.UtcNow,
                narration = narration
            };

            var summary = string.Join(" ", narration);
            return (errorPayload, new List<Artifact>(), summary);
        }
    }
}