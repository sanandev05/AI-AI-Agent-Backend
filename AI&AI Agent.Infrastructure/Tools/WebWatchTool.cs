using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class WebWatchTool : ITool
{
    public string Name => "WebWatch";
    public string Description => "Snapshots a URL's text and stores a hash for change tracking; can also compare current vs last snapshot.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            url = new { type = "string", description = "HTTP/HTTPS URL to watch." },
            action = new { type = "string", description = "'snapshot' to store latest, 'check' to compare with last snapshot." },
            maxWaitMs = new { type = "number", description = "Max wait for dynamic content (default 6000)." }
        },
        required = new[] { "url", "action" }
    };

    private readonly IBrowser _browser;
    private readonly AI_AI_Agent.Application.Services.IUrlSafetyService _urlSafety;
    private readonly string _storeDir;

    public WebWatchTool(IBrowser browser, AI_AI_Agent.Application.Services.IUrlSafetyService urlSafety)
    {
        _browser = browser;
        _urlSafety = urlSafety;
        _storeDir = Path.Combine(AppContext.BaseDirectory, "workspace", "webwatch");
        Directory.CreateDirectory(_storeDir);
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("url", out var u) || u.ValueKind != JsonValueKind.String) return "Error: url required.";
        if (!args.TryGetProperty("action", out var a) || a.ValueKind != JsonValueKind.String) return "Error: action required.";
        var url = u.GetString()!;
        var action = a.GetString()!.ToLowerInvariant();
        int maxWaitMs = args.TryGetProperty("maxWaitMs", out var mw) && mw.ValueKind == JsonValueKind.Number ? mw.GetInt32() : 6000;

        if (!_urlSafety.IsAllowed(url)) return "Blocked by URL policy.";

        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
        var path = Path.Combine(_storeDir, key + ".json");

        if (action == "snapshot" || action == "check")
        {
            var text = await ExtractTextAsync(url, maxWaitMs, ct);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text ?? string.Empty))).ToLowerInvariant();
            string? prevHash = null;
            DateTimeOffset? prevAt = null;
            if (File.Exists(path))
            {
                var prevJson = JsonDocument.Parse(File.ReadAllText(path));
                prevHash = prevJson.RootElement.GetProperty("hash").GetString();
                if (prevJson.RootElement.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.String)
                {
                    if (DateTimeOffset.TryParse(ts.GetString(), out var dto)) prevAt = dto;
                }
            }

            var changed = prevHash != null && prevHash != hash;
            var snapshot = new { url, hash, timestamp = DateTimeOffset.UtcNow };
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot));

            return new { message = action == "snapshot" ? "Snapshot stored" : "Checked", url, changed, previousHash = prevHash, previousAt = prevAt, currentHash = hash };
        }

        return "Error: action must be 'snapshot' or 'check'.";
    }

    private async Task<string> ExtractTextAsync(string url, int maxWaitMs, CancellationToken ct)
    {
        var context = await _browser.NewContextAsync(new() { ViewportSize = new() { Width = 1200, Height = 800 } });
        var page = await context.NewPageAsync();
        try
        {
            await page.GotoAsync(url, new PageGotoOptions { Timeout = 30000, WaitUntil = WaitUntilState.NetworkIdle });
            await page.WaitForTimeoutAsync(maxWaitMs);
            var body = await page.QuerySelectorAsync("body");
            if (body is null) return string.Empty;
            await body.EvaluateAsync("element => { const scripts = element.querySelectorAll('script, style, noscript'); scripts.forEach(s => s.remove()); }");
            return (await body.InnerTextAsync())?.Trim() ?? string.Empty;
        }
        finally
        {
            try { await page.CloseAsync(); } catch { }
            try { await context.CloseAsync(); } catch { }
        }
    }
}
