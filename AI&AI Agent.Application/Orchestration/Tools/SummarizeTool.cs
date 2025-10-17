using System.Text;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Tools;

public sealed class SummarizeTool : ITool
{
    public string Name => "Summarize";

    public Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        // Modes: "plain" (default), "research-notes", "final-synthesis"
        var mode = input.TryGetProperty("mode", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String ? (m.GetString() ?? "plain") : "plain";
        var bilingual = input.TryGetProperty("bilingual", out var b) && b.GetBoolean();
        var minWords = input.TryGetProperty("minWords", out var mw) && mw.ValueKind == System.Text.Json.JsonValueKind.Number ? Math.Max(0, mw.GetInt32()) : (mode == "final-synthesis" ? 150 : 50);

        // Optional: fromSteps ["s1","s2",...] — use their payloads
        var fromSteps = new List<string>();
        if (input.TryGetProperty("fromSteps", out var fs) && fs.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var idEl in fs.EnumerateArray())
            {
                if (idEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var sid = idEl.GetString(); if (!string.IsNullOrWhiteSpace(sid)) fromSteps.Add(sid!);
                }
            }
        }

        // Collect texts
        var texts = new List<string>();
        if (input.TryGetProperty("text", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var ts = t.GetString(); if (!string.IsNullOrWhiteSpace(ts)) texts.Add(ts!);
        }
        if (input.TryGetProperty("texts", out var ta) && ta.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in ta.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                { var sitem = item.GetString(); if (!string.IsNullOrWhiteSpace(sitem)) texts.Add(sitem!); }
            }
        }
        // Pull context payloads either from listed steps or all
        IEnumerable<object?> ctxPayloads = fromSteps.Count > 0
            ? fromSteps.Select(id => ctx.TryGetValue($"step:{id}:payload", out var p) ? p : null)
            : ctx.Where(kv => kv.Key.EndsWith(":payload")).Select(kv => kv.Value);

        // Also check for web content results
        if (ctx.TryGetValue("web_content:results", out var webResults) && webResults is IEnumerable<object> webResultsList)
        {
            ctxPayloads = ctxPayloads.Concat(webResultsList);
        }

        // Try to turn typical extract payloads into note lines
        var sources = new List<(string Site, string? Url, string Title, string ExcerptOrText)>();
        foreach (var p in ctxPayloads)
        {
            if (p is null) continue;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(p);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                string site = root.TryGetProperty("site", out var se) && se.ValueKind == System.Text.Json.JsonValueKind.String ? se.GetString() ?? string.Empty : string.Empty;
                string? url = root.TryGetProperty("url", out var ue) && ue.ValueKind == System.Text.Json.JsonValueKind.String ? ue.GetString() : null;
                string title = root.TryGetProperty("title", out var te) && te.ValueKind == System.Text.Json.JsonValueKind.String ? te.GetString() ?? string.Empty : string.Empty;
                string textOrExcerpt = root.TryGetProperty("text", out var tx) && tx.ValueKind == System.Text.Json.JsonValueKind.String ? (tx.GetString() ?? string.Empty)
                                        : (root.TryGetProperty("excerpt", out var ex) && ex.ValueKind == System.Text.Json.JsonValueKind.String ? (ex.GetString() ?? string.Empty) : string.Empty);
                if (!string.IsNullOrWhiteSpace(textOrExcerpt))
                {
                    sources.Add((string.IsNullOrWhiteSpace(site) ? (url is null ? "source" : new Uri(url).Host) : site, url, string.IsNullOrWhiteSpace(title) ? (url ?? site) : title, textOrExcerpt));
                }
                else if (root.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    texts.Add(root.GetString()!);
                }
            }
            catch
            {
                // Fallback: stringify
                texts.Add(p.ToString() ?? string.Empty);
            }
        }

        static string StripHtml(string s)
        {
            // minimal HTML removal to prevent raw markup in outputs
            try
            {
                return System.Text.RegularExpressions.Regex.Replace(s, "<[^>]+>", " ")
                    .Replace("&nbsp;", " ")
                    .Replace("&amp;", "&")
                    .Trim();
            }
            catch { return s; }
        }

        string BuildNotes()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Research Notes");
            sb.AppendLine(new string('-', 16));
            int idx = 1;
            foreach (var s in sources)
            {
                sb.AppendLine($"{idx}. {StripHtml(s.Title)} [{s.Site}]");
                // Simple heuristic: split into sentences and pick first 3-5
                var sentences = StripHtml(s.ExcerptOrText).Split(new[] {'.','!','?'}, StringSplitOptions.RemoveEmptyEntries)
                                               .Select(x => x.Trim()).Where(x => x.Length > 0).Take(5);
                foreach (var sent in sentences)
                {
                    sb.AppendLine($"   - {sent}.");
                }
                if (!string.IsNullOrWhiteSpace(s.Url)) sb.AppendLine($"   (Source: {s.Url})");
                sb.AppendLine();
                idx++;
            }
            if (texts.Count > 0)
            {
                sb.AppendLine("Additional Context");
                sb.AppendLine(new string('-', 18));
                foreach (var t in texts) sb.AppendLine($"- {t}");
            }
            return sb.ToString().Trim();
        }

        string BuildSmartSummary()
        {
            var sb = new StringBuilder();
            
            // Check if we have web content to summarize
            if (sources.Count > 0)
            {
                sb.AppendLine("Summary of Web Content:");
                sb.AppendLine(new string('=', 23));
                
                // Group by site for better organization
                var groupedSources = sources.GroupBy(s => s.Site).ToList();
                
                if (groupedSources.Count == 1)
                {
                    // Single site - provide detailed summary
                    var site = groupedSources[0].Key;
                    sb.AppendLine($"From {site}:");
                    sb.AppendLine();
                    
                    foreach (var source in groupedSources[0])
                    {
                        sb.AppendLine($"**{StripHtml(source.Title)}**");
                        
                        // Extract key points from content
                        var content = StripHtml(source.ExcerptOrText);
                        var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(x => x.Trim())
                                              .Where(x => x.Length > 20) // Filter out very short sentences
                                              .Take(8);
                        
                        foreach (var sentence in sentences)
                        {
                            sb.AppendLine($"• {sentence}.");
                        }
                        
                        if (!string.IsNullOrWhiteSpace(source.Url))
                        {
                            sb.AppendLine($"Source: {source.Url}");
                        }
                        sb.AppendLine();
                    }
                }
                else
                {
                    // Multiple sites - comparative summary
                    foreach (var group in groupedSources)
                    {
                        sb.AppendLine($"**{group.Key}:**");
                        
                        var combinedContent = string.Join(" ", group.Select(s => StripHtml(s.ExcerptOrText)));
                        var keyPoints = combinedContent.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(x => x.Trim())
                                                      .Where(x => x.Length > 15)
                                                      .Take(5);
                        
                        foreach (var point in keyPoints)
                        {
                            sb.AppendLine($"• {point}.");
                        }
                        sb.AppendLine();
                    }
                }
                
                // Add citations
                sb.AppendLine("**Sources:**");
                foreach (var source in sources.Where(s => !string.IsNullOrWhiteSpace(s.Url)))
                {
                    sb.AppendLine($"• [{source.Site}] {source.Url}");
                }
            }
            else if (texts.Count > 0)
            {
                // Fallback to text summarization
                sb.AppendLine("Summary:");
                sb.AppendLine(new string('=', 8));
                foreach (var text in texts.Take(10))
                {
                    sb.AppendLine($"• {StripHtml(text)}");
                }
            }
            else
            {
                sb.AppendLine("No content available to summarize.");
            }
            
            return sb.ToString().Trim();
        }

        string BuildFinal()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Final Synthesis");
            sb.AppendLine(new string('=', 15));
            // Aggregate a comparative view by site
            foreach (var grp in sources.GroupBy(s => s.Site))
            {
                sb.AppendLine($"\n{grp.Key}:");
                var lines = StripHtml(grp.First().ExcerptOrText).Split(new[] {'.','!','?'}, StringSplitOptions.RemoveEmptyEntries)
                                                      .Select(x => x.Trim()).Where(x => x.Length > 0).Take(4);
                foreach (var l in lines) sb.AppendLine($"- {l}.");
            }
            if (texts.Count > 0)
            {
                sb.AppendLine("\nContext:");
                foreach (var t in texts.Take(5)) sb.AppendLine($"- {StripHtml(t)}");
            }
            sb.AppendLine("\nCitations:");
            foreach (var s in sources)
            {
                if (!string.IsNullOrWhiteSpace(s.Url)) sb.AppendLine($"- [{s.Site}] {s.Url}");
            }
            return sb.ToString().Trim();
        }

        string output = mode switch
        {
            "research-notes" => BuildNotes(),
            "final-synthesis" => BuildFinal(),
            "smart" => BuildSmartSummary(),
            _ => sources.Count > 0 ? BuildSmartSummary() : string.Join("\n", texts)
        };

        // Enforce min words roughly
        if (minWords > 0)
        {
            var words = output.Split(new[] {' ','\n','\r','\t'}, StringSplitOptions.RemoveEmptyEntries).Length;
            if (words < minWords && (sources.Count > 0 || texts.Count > 0))
            {
                // Extend by appending more sentences from sources
                foreach (var s in sources)
                {
                    if (words >= minWords) break;
                    output += "\n" + s.ExcerptOrText;
                    words = output.Split(new[] {' ','\n','\r','\t'}, StringSplitOptions.RemoveEmptyEntries).Length;
                }
            }
        }

        if (output.Length > 32000) output = output.Substring(0, 32000);
        if (bilingual) output = $"EN Summary:\n{output}\n\nAZ Xülasə:\n{output}";
        return Task.FromResult(((object?)output, (IList<Artifact>)new List<Artifact>(), "Summary produced"));
    }
}
