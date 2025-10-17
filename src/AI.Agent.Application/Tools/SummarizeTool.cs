using System.Text;
using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.SemanticKernel;

namespace AI.Agent.Application.Tools;

public sealed class SummarizeTool : ITool
{
    public string Name => "Summarize";

    private readonly Kernel _kernel;
    public SummarizeTool(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var mode = input.TryGetProperty("mode", out var m) ? m.GetString() : "research-notes";
        var minWords = input.TryGetProperty("minWords", out var mw) && mw.ValueKind is JsonValueKind.Number ? mw.GetInt32() : GetDefaultMinWords(mode);
        var goal = input.TryGetProperty("goal", out var g) ? g.GetString() : "general analysis";

        var narration = new List<string>
        {
            $"🔍 Starting {mode} summarization",
            $"🎯 Goal: {goal}",
            $"📝 Minimum words: {minWords}"
        };

        // Read sources from fromSteps or fromStep
        var stepIds = new List<string>();
        if (input.TryGetProperty("fromSteps", out var fs) && fs.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in fs.EnumerateArray()) if (e.ValueKind == JsonValueKind.String) stepIds.Add(e.GetString()!);
        }
        else if (input.TryGetProperty("fromStep", out var f1) && f1.ValueKind == JsonValueKind.String)
        {
            stepIds.Add(f1.GetString()!);
        }

        narration.Add($"📂 Processing {stepIds.Count} source steps: {string.Join(", ", stepIds)}");

        var sources = new List<(string domain, string url, string text)>();
        foreach (var id in stepIds)
        {
            if (ctx.TryGetValue($"step:{id}:payload", out var payloadObj) && payloadObj is not null)
            {
                try
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(payloadObj);
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    var url = root.TryGetProperty("url", out var u) ? u.GetString() ?? string.Empty : string.Empty;
                    var text = root.TryGetProperty("text", out var t) ? t.GetString() ?? payloadObj.ToString() ?? string.Empty : (payloadObj.ToString() ?? string.Empty);
                    var domain = !string.IsNullOrWhiteSpace(url) ? new Uri(url).Host : $"step-{id}";
                    sources.Add((domain, url, text));
                    narration.Add($"📄 Loaded {text.Length:N0} characters from {domain}");
                }
                catch
                {
                    var text = payloadObj.ToString() ?? string.Empty;
                    sources.Add(($"step-{id}", string.Empty, text));
                    narration.Add($"📄 Loaded {text.Length:N0} characters from step {id}");
                }
            }
        }

        if (sources.Count == 0)
        {
            throw new ArgumentException($"No source data found from steps: {string.Join(", ", stepIds)}");
        }

        narration.Add($"✅ Successfully loaded {sources.Count} sources");

        string prompt = mode switch
        {
            "research-notes" => BuildResearchNotesPrompt(sources, goal),
            "final-synthesis" => BuildFinalSynthesisPrompt(sources, goal, minWords),
            "comparative-analysis" => BuildComparativeAnalysisPrompt(sources, goal, minWords),
            "analytical-report" => BuildAnalyticalReportPrompt(sources, goal, minWords),
            "creation-requirements" => BuildCreationRequirementsPrompt(sources, goal),
            "creative-synthesis" => BuildCreativeSynthesisPrompt(sources, goal, minWords),
            "browsing-summary" => BuildBrowsingSummaryPrompt(sources, goal),
            "comprehensive-report" => BuildComprehensiveReportPrompt(sources, goal, minWords),
            _ => BuildGeneralSummaryPrompt(sources, goal, minWords)
        };

        narration.Add($"🤖 Executing {mode} with specialized prompt");
        narration.Add($"📊 Total input: {sources.Sum(s => s.text.Length):N0} characters");

        var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
        var output = result?.ToString() ?? string.Empty;

        narration.Add($"📝 Generated output: {output.Length:N0} characters");

        // Validate output based on mode
        ValidateOutput(mode, output, minWords, sources, narration);

        narration.Add($"✅ {mode} completed successfully");

        var payload = new
        {
            mode = mode,
            content = output,
            sourceCount = sources.Count,
            outputLength = output.Length,
            minWords = minWords,
            goal = goal,
            narration = narration
        };

        return (payload, new List<Artifact>(), string.Join(" ", narration.TakeLast(3)));
    }

    private static int GetDefaultMinWords(string? mode) => mode switch
    {
        "final-synthesis" => 1000,
        "analytical-report" => 1200,
        "comprehensive-report" => 1200,
        "creative-synthesis" => 800,
        _ => 300
    };

    private static string BuildResearchNotesPrompt(List<(string domain, string url, string text)> sources, string? goal)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"You are a research analyst working on: {goal}

Produce STRICT JSON (no markdown) exactly matching:
{{""sections"": [{{""title"": string, ""bullets"": string[]}}], ""sources"": [{{""domain"": string, ""url"": string}}]}}

Rules:
- Remove boilerplate/navigation/ads/404 messages
- Each bullet ends with (Source: <domain>)
- Create multiple sections covering key aspects related to the goal
- Ensure sources list contains unique domains with their URLs if available
- Focus on information relevant to: {goal}

Materials:
{sb}";
    }

    private static string BuildFinalSynthesisPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Write a comprehensive, professional article about: {goal}

Format: Clean Markdown with H2/H3 headings
Length: Minimum {minWords} words
Structure: Introduction, multiple detailed sections, conclusion

Rules:
- Ignore navigation, 'skip to content', cookie banners, or 404 text
- Use inline citations as (Source: <domain>) after factual claims
- Provide depth and context appropriate for the topic
- Be accurate and informative
- No raw HTML or URLs in the body

Materials:
{sb}";
    }

    private static string BuildComparativeAnalysisPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Conduct a comparative analysis for: {goal}

Format: Structured Markdown report
Length: Minimum {minWords} words
Structure: Executive Summary, Comparison Matrix, Key Differences, Similarities, Recommendations, Conclusion

Rules:
- Compare and contrast information from different sources
- Identify patterns, differences, and commonalities
- Use citations (Source: <domain>) for all claims
- Provide analytical insights beyond just listing facts
- Draw meaningful conclusions

Materials:
{sb}";
    }

    private static string BuildAnalyticalReportPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Generate an analytical report on: {goal}

Format: Professional analytical document in Markdown
Length: Minimum {minWords} words
Structure: Executive Summary, Background, Analysis, Findings, Implications, Recommendations

Requirements:
- Deep analysis with insights and implications
- Data-driven conclusions where possible
- Professional tone and structure
- Comprehensive coverage of the topic
- Citations (Source: <domain>) for all factual claims

Materials:
{sb}";
    }

    private static string BuildCreationRequirementsPrompt(List<(string domain, string url, string text)> sources, string? goal)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Define requirements for creating: {goal}

Based on the source materials, identify:
- Essential components and features
- Technical specifications or standards
- Best practices and guidelines
- Common pitfalls to avoid
- Success criteria

Format: Structured requirements document
Use citations (Source: <domain>) for all recommendations

Materials:
{sb}";
    }

    private static string BuildCreativeSynthesisPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Create: {goal}

Using the provided materials as inspiration and guidance, create the requested item.
Length: Minimum {minWords} words
Format: Appropriate for the creation type (Markdown for documents, structured format for others)

Requirements:
- Incorporate insights from the source materials
- Be creative while maintaining accuracy
- Cite sources (Source: <domain>) where applicable
- Ensure the creation meets the goal requirements

Materials:
{sb}";
    }

    private static string BuildBrowsingSummaryPrompt(List<(string domain, string url, string text)> sources, string? goal)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Summarize browsing results for: {goal}

Create a comprehensive summary of the information found across the visited pages.
Structure: Key findings from each source, overall insights, relevance to the goal

Rules:
- Organize information logically
- Highlight the most important findings
- Note any inconsistencies or gaps
- Use citations (Source: <domain>)

Materials:
{sb}";
    }

    private static string BuildComprehensiveReportPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Generate a comprehensive report on: {goal}

Format: Complete professional report in Markdown
Length: Minimum {minWords} words
Structure: Executive Summary, Introduction, Main Sections (as appropriate), Analysis, Conclusions, Recommendations

Requirements:
- Thorough coverage of the topic
- Multiple perspectives and viewpoints
- Critical analysis and insights
- Professional presentation
- Extensive use of source citations (Source: <domain>)

Materials:
{sb}";
    }

    private static string BuildGeneralSummaryPrompt(List<(string domain, string url, string text)> sources, string? goal, int minWords)
    {
        var sb = new StringBuilder();
        foreach (var s in sources)
        {
            sb.AppendLine($"[Source: {s.domain}] {s.text}");
            sb.AppendLine();
        }

        return $@"Summarize the following information related to: {goal}

Length: Minimum {minWords} words
Format: Well-structured summary with clear sections

Rules:
- Extract key information relevant to the goal
- Organize logically with headings
- Remove irrelevant content (navigation, ads, etc.)
- Use citations (Source: <domain>)

Materials:
{sb}";
    }

    private static void ValidateOutput(string? mode, string output, int minWords, List<(string domain, string url, string text)> sources, List<string> narration)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"{mode} produced empty output");
        }

        var words = output.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words < minWords)
        {
            narration.Add($"⚠️ Output too short: {words} words < {minWords} required");
            throw new InvalidOperationException($"{mode} too short: {words} < {minWords} words");
        }

        narration.Add($"✅ Word count: {words} words (>= {minWords} required)");

        // For synthesis modes, check for source diversity
        if (mode is "final-synthesis" or "analytical-report" or "comprehensive-report")
        {
            var uniqueDomains = sources.Select(s => s.domain).Distinct().Count();
            if (uniqueDomains < 2)
            {
                narration.Add($"⚠️ Limited source diversity: {uniqueDomains} unique domains");
                throw new InvalidOperationException($"{mode} needs multiple diverse sources, found only {uniqueDomains}");
            }
            
            narration.Add($"✅ Source diversity: {uniqueDomains} unique domains");
        }
    }
}
