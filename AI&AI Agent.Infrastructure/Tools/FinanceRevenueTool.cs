using AI_AI_Agent.Application.Agent;
using AI_AI_Agent.Contract.Services;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Finds a company's latest annual revenue using multiple public sources (Macrotrends, Yahoo Finance, WSJ, Investor Relations),
/// avoids "Access Denied" pages by using alternative sources, and computes an estimated monthly revenue.
/// Returns structured data with sources and excerpts used for extraction.
/// </summary>
public sealed class FinanceRevenueTool : ITool
{
    public string Name => "FinanceRevenue";
    public string Description => "Finds latest annual revenue for a company from multiple sources (Macrotrends, Yahoo Finance, WSJ, Investor Relations), avoids blocked pages, and computes monthly estimate. Use when asked to find company revenue and monthly numbers.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            company = new { type = "string", description = "Company name, e.g., Tesla, Apple, Microsoft." },
            year = new { type = "number", description = "Target year; if omitted, use the latest found." },
            maxSources = new { type = "number", description = "Max sources to check (default 5)." }
        },
        required = new[] { "company" }
    };

    private readonly IGoogleSearchService _search;
    private readonly IBrowser _browser;
    private readonly AI_AI_Agent.Application.Services.IUrlSafetyService _urlSafety;

    public FinanceRevenueTool(IGoogleSearchService search, IBrowser browser, AI_AI_Agent.Application.Services.IUrlSafetyService urlSafety)
    {
        _search = search;
        _browser = browser;
        _urlSafety = urlSafety;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("company", out var cprop) || cprop.ValueKind != JsonValueKind.String)
        {
            return new { error = "'company' is required" };
        }
        var company = cprop.GetString()!.Trim();
        int? targetYear = args.TryGetProperty("year", out var yprop) && yprop.ValueKind == JsonValueKind.Number ? yprop.GetInt32() : (int?)null;
        int maxSources = args.TryGetProperty("maxSources", out var mprop) && mprop.ValueKind == JsonValueKind.Number ? Math.Max(1, mprop.GetInt32()) : 5;

        // Build diversified queries to avoid a single blocked source
        var queries = new List<string>
        {
            targetYear.HasValue ? $"{company} annual revenue {targetYear.Value}" : $"{company} annual revenue",
            $"{company} revenue Macrotrends",
            $"{company} revenue Yahoo Finance",
            $"{company} revenue WSJ",
            $"{company} investor relations revenue PDF"
        };

        // Search and gather candidate URLs
        var urls = new List<string>();
        foreach (var q in queries)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var sr = await _search.SearchAsync(q);
                if (sr.Results is null) continue;
                urls.AddRange(sr.Results.Select(r => r.Url).Where(u => !string.IsNullOrWhiteSpace(u))!);
            }
            catch { /* ignore search errors */ }
        }

        // Normalize and prioritize sources
        var normalized = urls
            .Select(u => u.Trim())
            .Where(u => u.StartsWith("http://") || u.StartsWith("https://"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string[] preferredHosts = new[] { "macrotrends.net", "finance.yahoo.com", "wsj.com", "investor", "ir.", "sec.gov" };
        normalized = normalized
            .OrderBy(u => PreferredRank(u, preferredHosts))
            .ThenBy(u => u.Length)
            .Take(maxSources)
            .ToList();

        var browse = new WebBrowseTool(_browser, _urlSafety);
        var findings = new List<RevenueFinding>();
        foreach (var url in normalized)
        {
            if (ct.IsCancellationRequested) break;
            if (!_urlSafety.IsAllowed(url)) continue;
            try
            {
                var argsObj = BuildBrowseArgs(url);
                var contentObj = await browse.InvokeAsync(argsObj, ct);
                var text = contentObj?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text)) continue;
                if (IsAccessDenied(text)) continue;

                var candidates = ExtractRevenueCandidates(text, targetYear);
                foreach (var c in candidates)
                {
                    findings.Add(c with { SourceUrl = url });
                }
            }
            catch { /* skip failed source */ }
        }

        if (findings.Count == 0)
        {
            return new { error = "No revenue data found from available sources. Try a different year or company." };
        }

        // Choose a sensible latest finding
        var nowYear = DateTime.UtcNow.Year;
        var chosen = findings
            .OrderByDescending(f => f.Year.HasValue && f.Year <= nowYear ? f.Year.Value : -1)
            .ThenByDescending(f => f.ValueUsd)
            .First();

        var annual = chosen.ValueUsd;
        var monthly = annual / 12.0;

        var sources = findings
            .OrderByDescending(f => f.ValueUsd)
            .ThenByDescending(f => f.Year ?? 0)
            .Take(5)
            .Select(f => new { url = f.SourceUrl, year = f.Year, excerpt = f.Excerpt })
            .ToList();

        return new
        {
            message = $"Estimated annual revenue for {company}{(chosen.Year.HasValue ? " (" + chosen.Year + ")" : string.Empty)}",
            company,
            year = chosen.Year,
            annualRevenueUSD = Math.Round(annual, 2),
            monthlyRevenueUSD = Math.Round(monthly, 2),
            unit = "USD",
            sources
        };
    }

    private static int PreferredRank(string url, string[] preferredHosts)
    {
        var lowered = url.ToLowerInvariant();
        for (int i = 0; i < preferredHosts.Length; i++)
        {
            if (lowered.Contains(preferredHosts[i])) return i;
        }
        return preferredHosts.Length + 1;
    }

    private static JsonElement BuildBrowseArgs(string url)
    {
        var payload = new Dictionary<string, object?>
        {
            ["url"] = url,
            ["maxWaitMs"] = 4000,
            ["fallback"] = true
        };
        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static bool IsAccessDenied(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("access denied") || t.Contains("permission to access") || t.Contains("forbidden") || t.Contains("captcha");
    }

    private static IEnumerable<RevenueFinding> ExtractRevenueCandidates(string text, int? targetYear)
    {
        var list = new List<RevenueFinding>();
        if (string.IsNullOrWhiteSpace(text)) return list;

        // Limit very large pages
        var sample = text.Length > 200_000 ? text.Substring(0, 200_000) : text;

        // Find candidate matches where "revenue" appears near a money amount
        var patterns = new[]
        {
            // e.g., Total revenue was $97.69 billion in 2024
            @"(?i)(?:total\s+)?revenue[^\n\r]{0,80}?\$?\s*([0-9]{1,3}(?:,[0-9]{3})*(?:\.[0-9]+)?)\s*(billion|million|bn|m|b|mm)?[^\n\r]{0,40}?(20\d{2})?",
            // e.g., Revenue: $97.69B
            @"(?i)revenue[^\n\r]{0,40}?\$?\s*([0-9]+(?:\.[0-9]+)?)\s*(b|m|bn|mm|billion|million)\b(?!\w)"
        };

        foreach (var pat in patterns)
        {
            foreach (Match m in Regex.Matches(sample, pat, RegexOptions.Singleline))
            {
                if (!m.Success) continue;
                var numStr = m.Groups[1].Value;
                var unitStr = m.Groups[2].Success ? m.Groups[2].Value : string.Empty;
                int? year = null;
                if (m.Groups.Count > 3 && m.Groups[3].Success)
                {
                    if (int.TryParse(m.Groups[3].Value, out var y)) year = y;
                }

                if (!TryParseMoney(numStr, unitStr, out var usd)) continue;

                // If targetYear is specified and matched year differs, de-prioritize via year = null
                if (targetYear.HasValue && year.HasValue && year.Value != targetYear.Value)
                {
                    year = null;
                }

                var excerpt = ExtractExcerpt(sample, m.Index, 180);
                list.Add(new RevenueFinding(usd, year, null, excerpt));
            }
        }
        return list;
    }

    private static bool TryParseMoney(string numStr, string unitRaw, out double usd)
    {
        usd = 0;
        if (string.IsNullOrWhiteSpace(numStr)) return false;
        var style = NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint;
        if (!double.TryParse(numStr.Replace(",", string.Empty), style, CultureInfo.InvariantCulture, out var val)) return false;
        var unit = (unitRaw ?? string.Empty).Trim().ToLowerInvariant();
        double mult = 1.0;
        if (unit == "b" || unit == "bn" || unit.Contains("billion")) mult = 1_000_000_000d;
        else if (unit == "m" || unit == "mm" || unit.Contains("million")) mult = 1_000_000d;
        else if (string.IsNullOrEmpty(unit)) {
            // If the number is very large, assume USD
            if (val > 10_000_000) mult = 1.0; // looks like raw dollars
            else return false; // ambiguous small number without unit
        }
        usd = val * mult;
        return usd > 100_000_000; // must be at least 100M to be plausible annual revenue
    }

    private static string ExtractExcerpt(string text, int index, int radius)
    {
        var start = Math.Max(0, index - radius);
        var len = Math.Min(text.Length - start, radius * 2);
        return text.Substring(start, len).Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private readonly record struct RevenueFinding(double ValueUsd, int? Year, string? SourceUrl, string Excerpt);
}
