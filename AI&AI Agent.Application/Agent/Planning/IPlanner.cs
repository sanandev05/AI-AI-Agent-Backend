using System.Text.Json;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using AI_AI_Agent.Application.Agent.Routing;
using AI_AI_Agent.Contracts;

namespace AI_AI_Agent.Application.Agent.Planning;

public interface IPlanner
{
    Task<Plan> CreatePlanAsync(string chatId, string goal, IReadOnlyList<(string role,string content)> history, CancellationToken ct);
    Task<Plan> MarkStepCompletedAsync(Plan plan, int stepId, string? rationale, CancellationToken ct);
}

public class LlmPlanner : IPlanner
{
    private readonly LLMRouter _router;
    public LlmPlanner(LLMRouter router) => _router = router;

    private const string PlanningInstruction = @"You are a planning assistant. Given a user goal and prior messages, produce a concise JSON plan.
Rules:
- JSON only. No extra text.
- Shape: { ""goal"": string, ""steps"": [ { ""id"": number, ""action"": string, ""rationale"": string } ] }
- 3-6 high-value steps.
- Avoid trivial steps like 'Think' or 'Start'.";

    public async Task<Plan> CreatePlanAsync(string chatId, string goal, IReadOnlyList<(string role, string content)> history, CancellationToken ct)
    {
        // Use router to pick backend (simple heuristic using history length)
        var backend = _router.GetBackend(goal, history.Count);
        var hist = history.ToList();
        var prompt = new StringBuilder();
        prompt.AppendLine("Goal: " + goal);
        prompt.AppendLine("Recent Context:");
        foreach (var h in hist.TakeLast(6))
            prompt.AppendLine("- " + (h.content?.Replace('\n',' ').Truncate(200) ?? string.Empty));
    // Provide empty prior assistant messages collection (depends on backend signature). Using Array.Empty<object>() as neutral.
    var emptyHistory = Enumerable.Empty<ChatMessageContent>();
    var result = await backend.CompleteAsync(PlanningInstruction, prompt.ToString(), emptyHistory, null, ct);
    var text = result.Text ?? string.Empty;
        // Try parse JSON strictly
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            var goalValue = root.TryGetProperty("goal", out var g) ? g.GetString() ?? goal : goal;
            var steps = new List<PlanStep>();
            if (root.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
            {
                int idx = 0;
                foreach (var s in stepsEl.EnumerateArray())
                {
                    idx++;
                    var id = s.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : idx;
                    var action = s.TryGetProperty("action", out var aEl) ? aEl.GetString() ?? $"Step {id}" : $"Step {id}";
                    var rationale = s.TryGetProperty("rationale", out var rEl) ? rEl.GetString() : null;
                    steps.Add(new PlanStep(id, action, rationale, PlanStepStatus.Pending));
                }
            }
            if (steps.Count == 0)
            {
                steps.Add(new PlanStep(1, goal, "Single-step fallback", PlanStepStatus.Pending));
            }
            return new Plan(chatId, goalValue, steps);
        }
        catch
        {
            // Fallback naive plan
            var fallback = new Plan(chatId, goal, new List<PlanStep>
            {
                new(1, $"Research: {goal.Truncate(60)}", "Gather key info"),
                new(2, "Synthesize response", "Organize findings"),
                new(3, "Produce final artifact if requested", "Generate deliverable")
            });
            return fallback;
        }
    }

    public Task<Plan> MarkStepCompletedAsync(Plan plan, int stepId, string? rationale, CancellationToken ct)
    {
        for (int i = 0; i < plan.Steps.Count; i++)
        {
            if (plan.Steps[i].Id == stepId)
            {
                plan.Steps[i] = plan.Steps[i] with { Status = PlanStepStatus.Completed, Rationale = rationale ?? plan.Steps[i].Rationale };
                break;
            }
        }
        return Task.FromResult(plan);
    }
}

internal static class StringExt
{
    public static string Truncate(this string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "...";
}
