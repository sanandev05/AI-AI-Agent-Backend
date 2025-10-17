namespace AI.Agent.Domain;

/// <summary>
/// A structured execution plan containing a high-level goal and an ordered list of steps.
/// </summary>
public record Plan(string Goal, IReadOnlyList<Step> Steps);

/// <summary>
/// A single step in a plan that invokes a named tool with a JSON input and success criteria.
/// </summary>
public record Step(
    string Id,
    string Tool,
    System.Text.Json.JsonElement Input,
    string Success,
    IReadOnlyList<string>? Deps = null
);

/// <summary>
/// Execution state for a step.
/// </summary>
public enum StepState
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}
