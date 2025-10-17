using System.Collections.Generic;
using AI_AI_Agent.Domain.Events;
using Microsoft.Playwright;

namespace AI_AI_Agent.Application.Tools;

public sealed class BrowserScreenshotTool : ITool
{
    public string Name => "Browser.Screenshot";
    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var url = input.TryGetProperty("url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("url is required");
        var selector = input.TryGetProperty("selector", out var s) && s.ValueKind == System.Text.Json.JsonValueKind.String ? s.GetString() : null;
        var fullPage = input.TryGetProperty("fullPage", out var fp) && fp.ValueKind == System.Text.Json.JsonValueKind.True;
        var ignoreHttpsErrors = input.TryGetProperty("ignoreHttpsErrors", out var ihe) ? (ihe.ValueKind == System.Text.Json.JsonValueKind.True || (ihe.ValueKind == System.Text.Json.JsonValueKind.Undefined)) : true;
    var userAgent = input.TryGetProperty("userAgent", out var ua) && ua.ValueKind == System.Text.Json.JsonValueKind.String ? ua.GetString() : null;
    userAgent ??= "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
        var timeoutMs = 60000;
        if (input.TryGetProperty("timeoutMs", out var to) && to.ValueKind == System.Text.Json.JsonValueKind.Number)
        { try { timeoutMs = to.GetInt32(); } catch { /* keep default */ } }
        var proxy = input.TryGetProperty("proxy", out var pr) && pr.ValueKind == System.Text.Json.JsonValueKind.String ? pr.GetString() : null;
        proxy ??= Environment.GetEnvironmentVariable("HTTPS_PROXY") ?? Environment.GetEnvironmentVariable("HTTP_PROXY");
        var extraHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (input.TryGetProperty("headers", out var hdrs) && hdrs.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            foreach (var p in hdrs.EnumerateObject())
            {
                if (p.Value.ValueKind == System.Text.Json.JsonValueKind.String)
                    extraHeaders[p.Name] = p.Value.GetString() ?? string.Empty;
            }
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs + 10_000));
        using var reg = cts.Token.Register(() => { });

        using var playwright = await Playwright.CreateAsync();
        var launchOpts = new BrowserTypeLaunchOptions { Headless = true };
        if (!string.IsNullOrWhiteSpace(proxy)) launchOpts.Proxy = new Proxy { Server = proxy };
        // Helpful flags for broader site compatibility; --no-sandbox harmless on Windows
        launchOpts.Args = new[] { "--disable-blink-features=AutomationControlled", "--no-sandbox" };
        await using var browser = await playwright.Chromium.LaunchAsync(launchOpts);

        var ctxOpts = new BrowserNewContextOptions { ViewportSize = null, IgnoreHTTPSErrors = ignoreHttpsErrors };
        if (!string.IsNullOrWhiteSpace(userAgent)) ctxOpts.UserAgent = userAgent;
        if (extraHeaders.Count > 0) ctxOpts.ExtraHTTPHeaders = extraHeaders;
        ctxOpts.Locale = "en-US";
        ctxOpts.TimezoneId = "UTC";
        var context = await browser.NewContextAsync(ctxOpts);
        // Mild anti-automation signal reduction
        await context.AddInitScriptAsync("Object.defineProperty(navigator, 'webdriver', { get: () => undefined });");
        var page = await context.NewPageAsync();
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeoutMs });
        // wait a bit longer for dynamic content
        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = Math.Min(timeoutMs, 30000) }); } catch { /* tolerate */ }
        if (!string.IsNullOrWhiteSpace(selector))
        {
            var element = await page.QuerySelectorAsync(selector);
            if (element is null) throw new InvalidOperationException($"Selector not found: {selector}");
            await element.ScreenshotAsync(new ElementHandleScreenshotOptions { Path = tmp, Timeout = timeoutMs });
        }
        else
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = tmp, FullPage = fullPage, Timeout = timeoutMs });
        }
        var fi = new FileInfo(tmp);
        var a = new Artifact(fi.Name, fi.FullName, "image/png", fi.Length);
        return (new { url, selector, fullPage }, new List<Artifact> { a }, $"Screenshot captured for {url}");
    }
}
