using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using AI_AI_Agent.Application.Services;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class WebBrowserTool : ITool
{
    public string Name => "WebBrowser";
    public string Description => "Navigate web pages with Playwright: goto, click, fill, screenshot, getText with proper timeouts and error handling.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            action = new { type = "string", description = "Action: 'goto', 'click', 'fill', 'screenshot', 'getText'" },
            url = new { type = "string", description = "URL for goto action" },
            selector = new { type = "string", description = "CSS selector for click/fill/getText" },
            text = new { type = "string", description = "Text to fill for fill action" },
            timeout = new { type = "number", description = "Timeout in seconds (default: 30)" }
        },
        required = new[] { "action" }
    };

    private readonly IBrowser _browser;
    private readonly IUrlSafetyService _urlSafety;
    private readonly ILogger<WebBrowserTool> _logger;
    private readonly string _workspacePath;

    public WebBrowserTool(IBrowser browser, IUrlSafetyService urlSafety, ILogger<WebBrowserTool> logger)
    {
        _browser = browser;
        _urlSafety = urlSafety;
        _logger = logger;
        _workspacePath = Path.Combine(AppContext.BaseDirectory, "workspace");
        Directory.CreateDirectory(_workspacePath);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var action = args.TryGetProperty("action", out var actionProp) ? actionProp.GetString() : null;
        var url = args.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        var selector = args.TryGetProperty("selector", out var selectorProp) ? selectorProp.GetString() : null;
        var text = args.TryGetProperty("text", out var textProp) ? textProp.GetString() : null;
        var timeoutSec = args.TryGetProperty("timeout", out var timeoutProp) ? timeoutProp.GetInt32() : 30;

        if (string.IsNullOrEmpty(action))
            return new { error = "Action is required", success = false };

        var timeout = timeoutSec * 1000; // Convert to milliseconds

        try
        {
            var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            var page = await context.NewPageAsync();
            
            // Anti-bot stealth
            await page.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                window.chrome = { runtime: {} };
                Object.defineProperty(navigator, 'languages', { get: () => ['en-US','en'] });
            ");

            try
            {
                switch (action.ToLowerInvariant())
                {
                    case "goto":
                        if (string.IsNullOrEmpty(url))
                            return new { error = "URL is required for goto action", success = false };
                        
                        if (!_urlSafety.IsAllowed(url))
                            return new { error = $"URL blocked by safety policy: {url}", success = false };

                        await page.GotoAsync(url, new PageGotoOptions 
                        { 
                            Timeout = timeout, 
                            WaitUntil = WaitUntilState.NetworkIdle 
                        });
                        
                        // Handle common cookie banners
                        await HandleCookieBanners(page);
                        
                        var title = await page.TitleAsync();
                        var currentUrl = page.Url;
                        return new { success = true, action, url = currentUrl, title, message = "Page loaded successfully" };

                    case "click":
                        if (string.IsNullOrEmpty(selector))
                            return new { error = "Selector is required for click action", success = false };
                        
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeout });
                        await page.ClickAsync(selector, new PageClickOptions { Timeout = timeout });
                        return new { success = true, action, selector, message = "Element clicked successfully" };

                    case "fill":
                        if (string.IsNullOrEmpty(selector) || string.IsNullOrEmpty(text))
                            return new { error = "Selector and text are required for fill action", success = false };
                        
                        await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeout });
                        await page.FillAsync(selector, text, new PageFillOptions { Timeout = timeout });
                        return new { success = true, action, selector, text, message = "Element filled successfully" };

                    case "screenshot":
                        var filename = $"screenshot_{DateTime.UtcNow:yyyyMMdd_HHmmss}.png";
                        var filepath = Path.Combine(_workspacePath, filename);
                        await page.ScreenshotAsync(new PageScreenshotOptions { Path = filepath, FullPage = true });
                        var downloadUrl = $"/api/files/{filename}";
                        return new { 
                            success = true, 
                            action, 
                            fileName = filename, 
                            filePath = filepath,
                            downloadUrl,
                            sizeBytes = new FileInfo(filepath).Length,
                            message = "Screenshot captured successfully" 
                        };

                    case "gettext":
                        string pageText;
                        if (!string.IsNullOrEmpty(selector))
                        {
                            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions { Timeout = timeout });
                            var element = await page.QuerySelectorAsync(selector);
                            pageText = await element.InnerTextAsync();
                        }
                        else
                        {
                            // Get all visible text from body
                            var body = await page.QuerySelectorAsync("body");
                            if (body != null)
                            {
                                await body.EvaluateAsync("element => { const scripts = element.querySelectorAll('script, style, noscript'); scripts.forEach(s => s.remove()); }");
                                pageText = (await body.InnerTextAsync())?.Trim() ?? "";
                            }
                            else
                            {
                                pageText = "";
                            }
                        }
                        
                        return new { 
                            success = true, 
                            action, 
                            selector = selector ?? "body", 
                            text = pageText,
                            length = pageText.Length,
                            message = $"Extracted {pageText.Length} characters of text" 
                        };

                    default:
                        return new { error = $"Unknown action: {action}", success = false };
                }
            }
            finally
            {
                await page.CloseAsync();
                await context.CloseAsync();
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "WebBrowser timeout for action {Action}", action);
            return new { error = $"Timeout after {timeoutSec}s for action {action}", success = false };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebBrowser error for action {Action}", action);
            return new { error = ex.Message, success = false };
        }
    }

    private async Task HandleCookieBanners(IPage page)
    {
        var cookieSelectors = new[]
        {
            "button[id*='cookie']:not([id*='reject'])",
            "button[class*='cookie']:not([class*='reject'])",
            "[data-testid*='accept-cookie']",
            "button:has-text('Accept')",
            "button:has-text('Allow')",
            "button:has-text('I agree')"
        };

        foreach (var selector in cookieSelectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null && await element.IsVisibleAsync())
                {
                    await element.ClickAsync();
                    await page.WaitForTimeoutAsync(1000); // Brief pause
                    break;
                }
            }
            catch
            {
                // Continue to next selector if this one fails
            }
        }
    }
}