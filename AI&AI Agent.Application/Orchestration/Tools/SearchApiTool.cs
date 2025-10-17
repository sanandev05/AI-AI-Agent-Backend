using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Tools;

public sealed class SearchApiTool : ITool
{
    public string Name => "SearchAPI.Query";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;

    public SearchApiTool(IHttpClientFactory http, IConfiguration cfg)
    { _http = http; _cfg = cfg; }

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var query = input.TryGetProperty("query", out var q) ? q.GetString() : null;
        var maxResults = input.TryGetProperty("maxResults", out var mr) && mr.ValueKind == System.Text.Json.JsonValueKind.Number ? mr.GetInt32() : 10;
        if (string.IsNullOrWhiteSpace(query)) throw new ArgumentException("query is required");

        var attempts = new List<string>();
        var list = await QueryAsync(query!, maxResults, ct);
        attempts.Add(query!);

        if (list.Count == 0)
        {
            var transliterated = Transliterate(query!);
            if (!string.Equals(transliterated, query, StringComparison.OrdinalIgnoreCase))
            {
                list = await QueryAsync(transliterated, maxResults, ct);
                attempts.Add(transliterated);
            }
        }

        if (list.Count == 0)
        {
            var english = TranslateKeywords(query!);
            if (!string.IsNullOrWhiteSpace(english) && !attempts.Contains(english, StringComparer.OrdinalIgnoreCase))
            {
                list = await QueryAsync(english, maxResults, ct);
                attempts.Add(english);
            }
        }

        if (list.Count == 0)
        {
            var generic = BuildFallback(query!);
            if (!string.IsNullOrWhiteSpace(generic) && !attempts.Contains(generic, StringComparer.OrdinalIgnoreCase))
            {
                list = await QueryAsync(generic, maxResults, ct);
                attempts.Add(generic);
            }
        }

        ctx["search:results"] = list;
        var attemptsSummary = string.Join(" → ", attempts);
        var payload = new { results = list, attempts };
        return (payload, new List<Artifact>(), $"SearchAPI returned {list.Count} results (attempts: {attemptsSummary})");
    }

    private async Task<IList<object>> QueryAsync(string query, int max, CancellationToken ct)
    {
        var provider = _cfg["Search:Provider"]?.Trim().ToLowerInvariant();
        return provider switch
        {
            "google-cse" or "google" => await GoogleAsync(query, max, ct),
            "serpapi" => await SerpApiAsync(query, max, ct),
            "bing" or "azure-bing" => await BingAsync(query, max, ct),
            _ => await GoogleAsync(query, max, ct)
        };
    }

    private async Task<IList<object>> GoogleAsync(string query, int max, CancellationToken ct)
    {
        // Google Custom Search JSON API (Programmable Search)
        var key = _cfg["Search:Google:ApiKey"];
        var cx = _cfg["Search:Google:Cx"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(cx))
            throw new InvalidOperationException("Google CSE not configured. Set Search:Google:ApiKey and Search:Google:Cx.");
        var http = _http.CreateClient();
        var url = $"https://www.googleapis.com/customsearch/v1?q={Uri.EscapeDataString(query)}&key={key}&cx={cx}&num={Math.Clamp(max,1,10)}";
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<object>();
        if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var it in items.EnumerateArray())
            {
                var title = it.TryGetProperty("title", out var t) ? t.GetString() : null;
                var link = it.TryGetProperty("link", out var l) ? l.GetString() : null;
                if (string.IsNullOrWhiteSpace(link)) continue;
                if (!Uri.TryCreate(link, UriKind.Absolute, out var u)) continue;
                var domain = u.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
                list.Add(new { title = title ?? domain, url = u.ToString(), domain });
                if (list.Count >= max) break;
            }
        }
        return list;
    }

    private async Task<IList<object>> BingAsync(string query, int max, CancellationToken ct)
    {
        var key = _cfg["Search:Bing:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Bing Web Search not configured. Set Search:Bing:ApiKey.");
        var http = _http.CreateClient();
        var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={Math.Clamp(max,1,50)}&mkt=en-US";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", key);
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<object>();
        if (doc.RootElement.TryGetProperty("webPages", out var wp) && wp.TryGetProperty("value", out var val) && val.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var it in val.EnumerateArray())
            {
                var name = it.TryGetProperty("name", out var n) ? n.GetString() : null;
                var link = it.TryGetProperty("url", out var l) ? l.GetString() : null;
                if (string.IsNullOrWhiteSpace(link)) continue;
                if (!Uri.TryCreate(link, UriKind.Absolute, out var u)) continue;
                var domain = u.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
                list.Add(new { title = name ?? domain, url = u.ToString(), domain });
                if (list.Count >= max) break;
            }
        }
        return list;
    }

    private async Task<IList<object>> SerpApiAsync(string query, int max, CancellationToken ct)
    {
        var key = _cfg["Search:SerpApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("SerpAPI not configured. Set Search:SerpApi:ApiKey.");
        var http = _http.CreateClient();
        var url = $"https://serpapi.com/search.json?engine=google&q={Uri.EscapeDataString(query)}&hl=en&num={Math.Clamp(max,1,10)}&api_key={key}";
        using var resp = await http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = System.Text.Json.JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var list = new List<object>();
        if (doc.RootElement.TryGetProperty("organic_results", out var items) && items.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var it in items.EnumerateArray())
            {
                var title = it.TryGetProperty("title", out var t) ? t.GetString() : null;
                var link = it.TryGetProperty("link", out var l) ? l.GetString() : null;
                if (string.IsNullOrWhiteSpace(link)) continue;
                if (!Uri.TryCreate(link, UriKind.Absolute, out var u)) continue;
                var domain = u.Host.Replace("www.", "", StringComparison.OrdinalIgnoreCase);
                list.Add(new { title = title ?? domain, url = u.ToString(), domain });
                if (list.Count >= max) break;
            }
        }
        return list;
    }

    private static string Transliterate(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return query;
        // Use case-sensitive comparer to allow distinct entries for lowercase/uppercase characters
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "ş", "sh" }, { "Ş", "Sh" },
            { "ç", "ch" }, { "Ç", "Ch" },
            { "ğ", "g" },  { "Ğ", "G" },
            { "ə", "e" },  { "Ə", "E" },
            { "ı", "i" },  { "İ", "I" },
            { "ö", "o" },  { "Ö", "O" },
            { "ü", "u" },  { "Ü", "U" }
        };
        var result = query;
        foreach (var kvp in map)
        {
            result = result.Replace(kvp.Key, kvp.Value, StringComparison.Ordinal);
        }
        return result;
    }

    private static string TranslateKeywords(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return query;
        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "haqda", "about" },
            { "məlumat", "information" },
            { "melumat", "information" },
            { "topla", "collect" },
            { "yaz", "write" },
            { "yazıb", "write" },
            { "yazib", "write" },
            { "yazlb", "write" },
            { "saxla", "save" },
            { "pdfdə", "pdf" },
            { "pdfde", "pdf" },
            { "mikrokontrolleri", "microcontroller" },
            { "mikrokontroller", "microcontroller" },
            { "mikroprosesor", "microprocessor" },
            { "hazırla", "prepare" },
            { "hazirla", "prepare" }
        };
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mapped = tokens.Select(t => replacements.TryGetValue(t, out var repl) ? repl : Transliterate(t)).ToArray();
        return string.Join(' ', mapped);
    }

    private static string BuildFallback(string query)
    {
        var ascii = Transliterate(query);
        if (string.IsNullOrWhiteSpace(ascii)) return string.Empty;
        // Prefer retaining core nouns; drop short function words
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pdf", "pptx", "docx", "guide", "tutorial" };
        var stop = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "və", "ve", "and", "the", "bir", "də", "de" };
        var terms = ascii.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => keep.Contains(t) || (!stop.Contains(t) && t.Length > 3))
            .Take(6)
            .ToArray();
        if (terms.Length == 0) return string.Empty;
        return $"{string.Join(' ', terms)} information overview";
    }
}
