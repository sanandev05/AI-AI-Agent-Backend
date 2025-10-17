using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Playwright;

namespace AI_AI_Agent.Infrastructure.Tools;

/// <summary>
/// Aggregates content from multiple web pages and returns structured extracts with a concise summary.
/// This tool is designed to help the model do multi-source research, producing citations and excerpts.
/// Note: Summarization here is lightweight and extractive; the model can refine it further if needed.
/// </summary>
public sealed class ResearchSummarizeTool : ITool
{
    public string Name => "ResearchSummarize";
    public string Description => "Fetches multiple URLs, extracts readable text via a headless browser with stealth, and returns structured excerpts with a brief extractive summary.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            urls = new { type = "array", items = new { type = "string" }, description = "List of HTTP/HTTPS URLs to aggregate." },
            perSourceMaxChars = new { type = "number", description = "Max characters to keep per source excerpt (default 3000)." },
            maxSources = new { type = "number", description = "Limit the number of sources to fetch (default all)." },
            summaryMaxChars = new { type = "number", description = "Max characters for the extractive summary (default 1500)." },
            waitMsPerSource = new { type = "number", description = "Additional wait time after load per source to allow dynamic content (default 1500ms)." },
            createDocx = new { type = "boolean", description = "If true, also create a DOCX report containing the summary and citations." },
            fileName = new { type = "string", description = "Optional file name for the DOCX (must end with .docx)." },
            title = new { type = "string", description = "Optional title for the DOCX report." }
        },
        required = new[] { "urls" }
    };

    private readonly IBrowser _browser;
    private readonly AI_AI_Agent.Application.Services.IUrlSafetyService _urlSafety;

    public ResearchSummarizeTool(IBrowser browser, AI_AI_Agent.Application.Services.IUrlSafetyService urlSafety)
    {
        _browser = browser;
        _urlSafety = urlSafety;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken)
    {
        if (!args.TryGetProperty("urls", out var urlsProp) || urlsProp.ValueKind != JsonValueKind.Array)
        {
            return "Error: 'urls' must be an array of strings.";
        }

        var urls = new List<string>();
        foreach (var el in urlsProp.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                var u = el.GetString();
                if (!string.IsNullOrWhiteSpace(u)) urls.Add(u!);
            }
        }
        if (urls.Count == 0) return "Error: 'urls' is empty.";

        int perSourceMax = args.TryGetProperty("perSourceMaxChars", out var psm) && psm.ValueKind == JsonValueKind.Number ? psm.GetInt32() : 3000;
        int summaryMax = args.TryGetProperty("summaryMaxChars", out var sm) && sm.ValueKind == JsonValueKind.Number ? sm.GetInt32() : 1500;
        int? maxSources = args.TryGetProperty("maxSources", out var ms) && ms.ValueKind == JsonValueKind.Number ? ms.GetInt32() : null;
    int waitMsPerSource = args.TryGetProperty("waitMsPerSource", out var wps) && wps.ValueKind == JsonValueKind.Number ? wps.GetInt32() : 1500;
    bool createDocx = args.TryGetProperty("createDocx", out var cd) && cd.ValueKind == JsonValueKind.True;
    string? fileName = args.TryGetProperty("fileName", out var fn) && fn.ValueKind == JsonValueKind.String ? fn.GetString() : null;
    string? title = args.TryGetProperty("title", out var ti) && ti.ValueKind == JsonValueKind.String ? ti.GetString() : null;

        if (maxSources.HasValue && maxSources.Value > 0 && urls.Count > maxSources.Value)
        {
            urls = urls.Take(maxSources.Value).ToList();
        }

        // Reuse WebBrowseTool behavior for stealth + extraction
    var browse = new WebBrowseTool(_browser, _urlSafety);

        var results = new List<SourceResult>();
        foreach (var url in urls)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                if (!_urlSafety.IsAllowed(url))
                {
                    results.Add(new SourceResult(url, $"[Blocked by policy] {_urlSafety.GetViolationReason(url) ?? "URL not allowed"}"));
                    continue;
                }
                // Use fallback raw HTTP if browser blocked; give the page a bit of time to render
                var browseArgs = BuildArgs(url, waitMsPerSource);
                var obj = await browse.InvokeAsync(browseArgs, cancellationToken);
                var content = obj?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(content))
                {
                    results.Add(new SourceResult(url, ""));
                    continue;
                }

                var excerpt = TrimToExcerpt(content, perSourceMax);
                results.Add(new SourceResult(url, excerpt));
            }
            catch (Exception ex)
            {
                results.Add(new SourceResult(url, $"[Error extracting content: {ex.Message} ]"));
            }
        }

        var combined = string.Join("\n\n---\n\n", results.Select(r => $"Source: {r.Url}\n{r.Excerpt}"));
        var summary = BuildExtractiveSummary(results, summaryMax);

        var payload = new
        {
            message = "Aggregated research across sources",
            sources = results.Select(r => new { url = r.Url, excerpt = r.Excerpt, length = r.Excerpt?.Length ?? 0 }).ToList(),
            combinedLength = combined.Length,
            summary
        };
        if (createDocx)
        {
            var safeTitle = string.IsNullOrWhiteSpace(title) ? "Research Summary" : title!;
            var safeFile = string.IsNullOrWhiteSpace(fileName) ?
                $"research_summary_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}.docx" :
                (fileName!.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? fileName! : fileName! + ".docx");

            var docContent = new StringBuilder();
            docContent.AppendLine("Summary:");
            docContent.AppendLine(summary);
            docContent.AppendLine();
            docContent.AppendLine("Sources:");
            int i = 1;
            foreach (var s in results)
            {
                if (string.IsNullOrWhiteSpace(s.Url)) continue;
                docContent.AppendLine($"[{i}] {s.Url}");
                i++;
            }

            var docx = new DocxCreateTool();
            var (path, size) = docx.CreateDocument(safeFile, docContent.ToString(), safeTitle);
            return new
            {
                message = "Aggregated research across sources and created DOCX",
                sources = payload.sources,
                combinedLength = payload.combinedLength,
                summary = payload.summary,
                file = new { fileName = safeFile, title = safeTitle, sizeBytes = size, path, downloadUrl = $"/api/files/{safeFile}" }
            };
        }
        return payload;
    }

    private static JsonElement BuildArgs(string url, int waitMs)
    {
        var payload = new Dictionary<string, object?>
        {
            ["url"] = url,
            ["maxWaitMs"] = Math.Max(0, waitMs),
            ["fallback"] = true
        };
        var json = JsonSerializer.Serialize(payload);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static string TrimToExcerpt(string content, int maxChars)
    {
        if (string.IsNullOrEmpty(content)) return string.Empty;
        var trimmed = content.Trim();
        if (trimmed.Length <= maxChars) return trimmed;
        return trimmed.Substring(0, Math.Max(0, maxChars)) + "…";
    }

    private static string BuildExtractiveSummary(IEnumerable<SourceResult> sources, int maxChars)
    {
        // Simple extractive approach: take first few strong sentences across sources, preferring longer ones, until cap
        var sentences = new List<string>();
        foreach (var s in sources)
        {
            if (string.IsNullOrWhiteSpace(s.Excerpt)) continue;
            var splits = SplitSentences(s.Excerpt).Take(5); // up to 5 per source
            sentences.AddRange(splits);
        }
        // Rank sentences by length (proxy for information density) and uniqueness
        var ranked = sentences
            .Select(x => x.Trim())
            .Where(x => x.Length > 40)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .Take(20)
            .ToList();

        var sb = new StringBuilder();
        foreach (var s in ranked)
        {
            if (sb.Length + s.Length + 1 > maxChars) break;
            sb.AppendLine("• " + s);
        }
        if (sb.Length == 0)
        {
            // Fallback: take the very beginning of first source
            var firstExcerpt = sources.FirstOrDefault().Excerpt ?? string.Empty;
            return TrimToExcerpt(firstExcerpt, maxChars);
        }
        return sb.ToString().Trim();
    }

    private static IEnumerable<string> SplitSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var delimiters = new[] { ". ", "? ", "! " };
        int start = 0;
        for (int i = 0; i < text.Length - 1; i++)
        {
            char c = text[i];
            if (c == '.' || c == '?' || c == '!')
            {
                int end = i + 1;
                if (end - start > 20) // avoid super short fragments
                {
                    yield return text.Substring(start, end - start).Trim();
                }
                start = end + 1;
            }
        }
        if (start < text.Length)
        {
            var tail = text.Substring(start).Trim();
            if (tail.Length > 20) yield return tail;
        }
    }

    private readonly record struct SourceResult(string Url, string Excerpt);
}
