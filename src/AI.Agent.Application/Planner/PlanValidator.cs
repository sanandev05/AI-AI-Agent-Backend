using System.Text.Json;
using AI.Agent.Domain;

namespace AI.Agent.Application.Planner;

public sealed class PlanValidator
{
    private readonly HashSet<string> _allowed;

    public PlanValidator(IEnumerable<ITool> tools)
    {
        _allowed = tools.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public Plan Validate(string goal, IEnumerable<Step> steps)
    {
        var list = new List<Step>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var i = 0;
        foreach (var s in steps)
        {
            if (!_allowed.Contains(s.Tool)) continue; // drop unknown tool

            var id = string.IsNullOrWhiteSpace(s.Id) ? $"s{++i}" : s.Id;
            if (!seenIds.Add(id)) id = $"s{++i}";

            var input = s.Input.ValueKind == JsonValueKind.Object ? s.Input : EmptyObject();
            var success = string.IsNullOrWhiteSpace(s.Success) ? $"{s.Tool} completed" : s.Success;
            var deps = s.Deps is null ? Array.Empty<string>() : s.Deps;

            list.Add(new Step(id, s.Tool, input, success, deps));
        }

        // De-duplicate exact duplicates (same Tool + Input + Success)
        var dedup = new List<Step>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in list)
        {
            var key = $"{s.Tool}|{s.Input.GetRawText()}|{s.Success}";
            if (seen.Add(key)) dedup.Add(s);
        }

        return new Plan(goal, dedup);
    }

    private static JsonElement EmptyObject()
    {
        using var doc = JsonDocument.Parse("{}");
        return doc.RootElement.Clone();
    }
}
