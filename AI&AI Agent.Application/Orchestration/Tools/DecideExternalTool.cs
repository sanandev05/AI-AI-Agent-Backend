using System.Text;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Tools;

/// <summary>
/// Emits a thinking process explaining whether outside help (web search/tools) is required
/// for the given goal, using deterministic heuristics only (no network calls).
/// </summary>
public sealed class DecideExternalTool : ITool
{
    public string Name => "Think.DecideExternal";

    public Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var goal = input.TryGetProperty("goal", out var g) && g.ValueKind == System.Text.Json.JsonValueKind.String ? (g.GetString() ?? string.Empty) : string.Empty;
        var goalLower = goal.ToLowerInvariant();

        bool hasUrl = System.Text.RegularExpressions.Regex.IsMatch(goalLower, @"https?://[^\s]+");
        bool mentionsDomain = System.Text.RegularExpressions.Regex.IsMatch(goalLower, @"\b([a-z0-9\-]+\.)+[a-z]{2,}\b");
        var freshness = new[] { "today", "latest", "current", "news", "up to date", "recent" };
        var webby = new[] { "from site", "from url", "visit", "browse", "check website", "analyze website", "web page", "screenshot" };
        var retrieval = new[] { "find", "search", "compare", "price", "prices", "documentation", "docs", "specs", "data sheet", "manual" };
        var calculation = new[] { "+", "-", "*", "/", "plus", "minus", "times", "divided by", "calculate", "sum", "difference", "product", "quotient" };

        bool needsFreshness = freshness.Any(k => goalLower.Contains(k));
        bool needsWebby = webby.Any(k => goalLower.Contains(k));
        bool needsRetrieval = retrieval.Any(k => goalLower.Contains(k));
        bool isSimpleCalc = calculation.Any(k => goalLower.Contains(k)) && goalLower.Length < 64;

        bool needExternal = hasUrl || mentionsDomain || needsFreshness || needsWebby || needsRetrieval;
        if (isSimpleCalc) needExternal = false;

        var sb = new StringBuilder();
        sb.AppendLine("Thinking process");
        sb.AppendLine();
        sb.AppendLine("• Processing User Query");
        sb.AppendLine($"  The user's goal is: '{goal}'.");
        if (isSimpleCalc)
        {
            sb.AppendLine("  This appears to be a direct calculation; no outside help is required.");
        }
        else
        {
            if (hasUrl) sb.AppendLine("  Contains a direct URL → outside help likely needed.");
            if (mentionsDomain) sb.AppendLine("  Mentions a domain/site → outside help likely needed.");
            if (needsFreshness) sb.AppendLine("  Requests current/latest info → outside help likely needed.");
            if (needsWebby) sb.AppendLine("  Mentions browsing/site analysis → outside help likely needed.");
            if (needsRetrieval) sb.AppendLine("  Requires finding/comparing documentation or prices → outside help likely needed.");
        }
        sb.AppendLine();
        sb.AppendLine("• Defining Task Simplicity");
        sb.AppendLine(isSimpleCalc
            ? "  The task is simple and can be answered locally."
            : "  The task likely benefits from external sources.");

        var payload = new { needExternal, goal, reasons = sb.ToString() };
        var summary = needExternal ? "External help recommended." : "External help not required.";

        // Save to context for planner/executor use if desired
        ctx["think:needExternal"] = needExternal;
        ctx["think:reasons"] = sb.ToString();

        return Task.FromResult(((object?)payload, (IList<Artifact>)Array.Empty<Artifact>(), summary));
    }
}
