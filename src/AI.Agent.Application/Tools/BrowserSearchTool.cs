using System.Text.Json;
using AI.Agent.Domain.Events;
using AI.Agent.Application.Search;

namespace AI.Agent.Application.Tools;

public sealed class BrowserSearchTool : ITool
{
    public string Name => "Browser.Search";

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        // Inputs
        var rawQuery = input.TryGetProperty("query", out var q) ? q.GetString() ?? string.Empty : string.Empty;
        var maxResults = input.TryGetProperty("maxResults", out var mr) ? Math.Clamp(mr.GetInt32(), 1, 20) : 10;
        var allowlist = new List<string>();
        if (input.TryGetProperty("domainAllowlist", out var da) && da.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in da.EnumerateArray())
            {
                var d = el.GetString();
                if (!string.IsNullOrWhiteSpace(d)) allowlist.Add(d!);
            }
        }

        // Normalize query based on simple heuristics
        var (normalized, allow) = AI.Agent.Application.Search.QueryBuilder.Normalize(rawQuery, allowlist.ToArray());

        // Stub search results (replace with real provider if desired)
        // We'll just return the top allowlisted candidate from the query text hints
        var results = new List<object>();
        if (allowlist.Count > 0)
        {
            foreach (var domain in allowlist)
            {
                results.Add(new { title = $"Result for {normalized}", url = $"https://{domain}/search?q={Uri.EscapeDataString(normalized)}", domain });
                if (results.Count >= maxResults) break;
            }
        }
        else
        {
            // Fall back to example.com placeholder
            results.Add(new { title = $"Result for {normalized}", url = $"https://www.bing.com/search?q={Uri.EscapeDataString(normalized)}", domain = "bing.com" });
        }

        // Filter by allowlisted domains if provided
        if (allow.Length > 0)
        {
            results = results.Where(r =>
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(r);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var domain = doc.RootElement.TryGetProperty("domain", out var d) ? d.GetString() ?? string.Empty : string.Empty;
                    return allow.Any(a => domain.EndsWith(a, StringComparison.OrdinalIgnoreCase));
                }
                catch { return false; }
            }).ToList();
        }

        // Persist in context for deterministic handoffs
        ctx["search:lastResults"] = results;
        if (results.Count > 0)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(results[0]);
                using var doc0 = System.Text.Json.JsonDocument.Parse(json);
                var firstUrl = doc0.RootElement.TryGetProperty("url", out var u0) ? u0.GetString() : null;
                if (!string.IsNullOrWhiteSpace(firstUrl)) ctx["nav:url"] = firstUrl!;
            }
            catch { /* ignore */ }
        }

        var payload = new { query = rawQuery, normalized, results };
        var summary = $"Search returned {results.Count} results. First URL persisted to nav:url.";
        return (payload, new List<Artifact>(), summary);
    }
}
