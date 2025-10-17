namespace AI_AI_Agent.Domain;

public record Plan(string Goal, IReadOnlyList<Step> Steps);
public record Step(string Id, string Tool, System.Text.Json.JsonElement Input,
                   string Success, IReadOnlyList<string>? Deps = null);
public enum StepState { Pending, Running, Succeeded, Failed, Skipped }

