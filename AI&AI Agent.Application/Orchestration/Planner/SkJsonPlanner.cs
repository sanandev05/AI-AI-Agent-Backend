using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI_AI_Agent.Domain;
using Microsoft.SemanticKernel;

namespace AI_AI_Agent.Application.Planner;

/// <summary>
/// Semantic Kernel-based planner that returns a strict JSON Plan using only allowed tools.
/// Falls back to a deterministic, non-example.com plan when no chat service is configured.
/// </summary>
public sealed class SkJsonPlanner : IPlanner
{
    private readonly Kernel _kernel;
    private readonly string[] _allowedTools;

    public SkJsonPlanner(Kernel kernel, IEnumerable<ITool> tools)
    {
        _kernel = kernel;
        _allowedTools = tools.Select(t => t.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<Plan> MakePlanAsync(string goal, CancellationToken ct)
    {
        try
        {
            var prompt = BuildPrompt(goal, _allowedTools);
            // Invoke the default chat completion service via simple prompt
            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var text = result?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Planner returned empty response");

            // Some models may wrap in code fences; strip them defensively
            text = StripCodeFences(text);
            var plan = ParsePlan(text, _allowedTools);
            return plan with { Goal = goal };
        }
        catch
        {
            // Fallback: produce a reasonable, non-example.com plan based on the goal
            return FallbackPlan(goal);
        }
    }

    private static string BuildPrompt(string goal, string[] allowed)
    {
        var tools = string.Join(", ", allowed.Select(a => $"\"{a}\""));
        var sb = new StringBuilder();
        sb.AppendLine("You are a planner that outputs ONLY JSON matching this C#-like record schema (no markdown, no commentary):");
        sb.AppendLine("Plan: { goal: string, steps: Step[] }");
        sb.AppendLine("Step: { id: string, tool: string, input: object, success: string, deps?: string[] }");
        sb.AppendLine();
    sb.AppendLine("Rules:");
    sb.AppendLine("- Use only these tools: " + tools + ".");
    sb.AppendLine("- Do NOT use example.com; choose real, relevant URLs.");
    sb.AppendLine("- Preferred flow for research/comparison tasks: \n  1) Browser.Search { query } -> 2) 2-4 x Browser.Extract { url } from top distinct domains -> 3) Summarize { mode=\"research-notes\", fromSteps=[extract step ids] } -> 4) Summarize { mode=\"final-synthesis\", fromSteps=[notes step id], minWords>=150 } -> 5) Docx.Create { title, bodyFromStep=final step id }.");
    sb.AppendLine("- For visual evidence, optionally add 1-2 Browser.Screenshot steps of key pages (fullPage=true).");
    sb.AppendLine("- Keep step ids short (s0, s1...).\n- input must be valid JSON for each tool.");
        sb.AppendLine();
        sb.AppendLine("Goal:");
        sb.AppendLine(goal);
        sb.AppendLine();
        sb.AppendLine("Return JSON ONLY.");
        return sb.ToString();
    }

    private static string StripCodeFences(string s)
    {
        s = s.Trim();
        if (s.StartsWith("```"))
        {
            var idx = s.IndexOf('\n');
            if (idx >= 0) s = s[(idx + 1)..];
            if (s.EndsWith("```")) s = s[..^3];
        }
        return s.Trim();
    }

    private static Plan ParsePlan(string json, string[] allowed)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var goal = root.TryGetProperty("goal", out var g) && g.ValueKind == JsonValueKind.String ? g.GetString() ?? string.Empty : string.Empty;
        var stepsEl = root.GetProperty("steps");
        var steps = new List<Step>();
        foreach (var s in stepsEl.EnumerateArray())
        {
            var id = s.GetProperty("id").GetString() ?? throw new InvalidOperationException("step.id required");
            var tool = s.GetProperty("tool").GetString() ?? throw new InvalidOperationException("step.tool required");
            if (!allowed.Contains(tool, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException($"Unknown tool: {tool}");
            // Clone the JsonElement so the backing JsonDocument can be disposed safely after parsing
            var input = s.GetProperty("input").Clone();
            var success = s.GetProperty("success").GetString() ?? "ok";
            IReadOnlyList<string>? deps = null;
            if (s.TryGetProperty("deps", out var d) && d.ValueKind == JsonValueKind.Array)
            {
                deps = d.EnumerateArray().Select(x => x.GetString() ?? string.Empty).Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
            }
            steps.Add(new Step(id, tool, input, success, deps));
        }
        return new Plan(goal, steps);
    }

    private static Plan FallbackPlan(string goal)
    {
        // Build a deterministic research plan using search -> extract x3 -> notes -> final -> docx
        string Enc(string q) => Uri.EscapeDataString(q);
        var drop = new HashSet<string>(new[] { "compare","vs","from","sites","site","the","a","an","and","or","prices","price","cost" }, StringComparer.OrdinalIgnoreCase);
        var tokens = goal.Split(new[] { ' ', ',', '.', ':', ';', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                         .Where(t => t.Length > 2 && !drop.Contains(t))
                         .Take(6);
        var q = string.Join(' ', tokens);
        var steps = new List<Step>();

        // s0: Search
        using (var s0Doc = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { query = q, maxResults = 10 })))
        {
            steps.Add(new Step("s0", "Browser.Search", s0Doc.RootElement.Clone(), "Search results obtained"));
        }
        // s1-s3: Extract from known retail domains using fixed URLs as a fallback approximation
        var extractUrls = new[]
        {
            $"https://www.walmart.com/search?q={Enc(q)}",
            $"https://www.bestbuy.com/site/searchpage.jsp?st={Enc(q)}",
            $"https://www.ebay.com/sch/i.html?_nkw={Enc(q)}",
        };
        for (int i = 0; i < extractUrls.Length; i++)
        {
            using var d = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { url = extractUrls[i], selector = "main, article, #content, body", timeoutSec = 30 }));
            steps.Add(new Step($"s{i+1}", "Browser.Extract", d.RootElement.Clone(), "Content extracted", new[] { "s0" }));
        }
        // s4: Notes
        using (var s4 = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { mode = "research-notes", fromSteps = new[] { "s1", "s2", "s3" }, minWords = 120 })))
        {
            steps.Add(new Step("s4", "Summarize", s4.RootElement.Clone(), "Notes compiled", new[] { "s1", "s2", "s3" }));
        }
        // s5: Final synthesis
        using (var s5 = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { mode = "final-synthesis", fromSteps = new[] { "s4" }, minWords = 180 })))
        {
            steps.Add(new Step("s5", "Summarize", s5.RootElement.Clone(), "Final synthesis ready", new[] { "s4" }));
        }
        // s6: Docx
        using (var s6 = JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new { title = $"Report: {goal}", bodyFromStep = "s5" })))
        {
            steps.Add(new Step("s6", "Docx.Create", s6.RootElement.Clone(), "DOCX created", new[] { "s5" }));
        }
        return new Plan(goal, steps);
    }
}
