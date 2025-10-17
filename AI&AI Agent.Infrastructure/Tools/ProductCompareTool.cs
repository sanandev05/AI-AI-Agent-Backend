using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class ProductCompareTool : ITool
{
    public string Name => "ProductCompare";
    public string Description => "Fetch product pages and extract approximate price and rating, then compare and rank.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            urls = new { type = "array", items = new { type = "string" } },
            maxWaitMs = new { type = "number" }
        },
        required = new[] { "urls" }
    };

    private readonly IBrowser _browser;
    private readonly AI_AI_Agent.Application.Services.IUrlSafetyService _urlSafety;

    public ProductCompareTool(IBrowser browser, AI_AI_Agent.Application.Services.IUrlSafetyService urlSafety)
    {
        _browser = browser; _urlSafety = urlSafety;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var urls = args.GetProperty("urls").EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
        int maxWaitMs = args.TryGetProperty("maxWaitMs", out var mw) && mw.ValueKind == JsonValueKind.Number ? mw.GetInt32() : 6000;
        if (urls.Count == 0) return "Error: urls empty.";

        var browse = new WebBrowseTool(_browser, _urlSafety);
        var items = new List<Item>();
        foreach (var url in urls)
        {
            if (ct.IsCancellationRequested) break;
            if (!_urlSafety.IsAllowed(url)) { items.Add(new Item(url, null, null, "blocked")); continue; }
            var payload = BuildArgs(url, maxWaitMs);
            var textObj = await browse.InvokeAsync(payload, ct);
            var text = textObj?.ToString() ?? string.Empty;
            var price = ExtractPrice(text);
            var rating = ExtractRating(text);
            items.Add(new Item(url, price, rating, null));
        }

        var comparable = items.Where(i => i.Price.HasValue).OrderBy(i => i.Price).ToList();
        var tiers = new
        {
            budget = comparable.Take(Math.Max(1, comparable.Count / 3)).ToList(),
            mid = comparable.Skip(Math.Max(1, comparable.Count / 3)).Take(Math.Max(1, comparable.Count / 3)).ToList(),
            premium = comparable.Skip(Math.Max(2, 2 * comparable.Count / 3)).ToList()
        };

        return new { message = "Product comparison", items, tiers };
    }

    private static JsonElement BuildArgs(string url, int maxWait)
    {
        var o = new Dictionary<string, object?> { ["url"] = url, ["maxWaitMs"] = maxWait, ["fallback"] = true };
        var json = JsonSerializer.Serialize(o);
        using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone();
    }

    private static double? ExtractPrice(string text)
    {
        // Match currency like $1,234.56 or 1,234.56 USD
        var rx = new Regex(@"(?:(?:USD|US\$|\$)\s?)([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)|([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]{2})?)\s?(?:USD|US\$|\$)", RegexOptions.IgnoreCase);
        var m = rx.Match(text);
        if (!m.Success) return null;
        var num = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
        if (double.TryParse(num.Replace(",", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return d;
        return null;
    }

    private static double? ExtractRating(string text)
    {
        var rx = new Regex(@"([0-5](?:\.[0-9])?)\s*out\s*of\s*5", RegexOptions.IgnoreCase);
        var m = rx.Match(text);
        if (!m.Success) return null;
        if (double.TryParse(m.Groups[1].Value, out var d)) return d;
        return null;
    }

    public record Item(string Url, double? Price, double? Rating, string? Note);
}
