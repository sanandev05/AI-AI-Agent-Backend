namespace AI_AI_Agent.Application.Agent.Planning;

public enum PlanStepStatus
{
    Pending,
    InProgress,
    Completed,
    Skipped,
    Failed
}

public record PlanStep(int Id, string Action, string? Rationale, PlanStepStatus Status = PlanStepStatus.Pending);

public record Plan(string ChatId, string Goal, IList<PlanStep> Steps)
{
    public PlanStep? GetNextPending() => Steps.FirstOrDefault(s => s.Status == PlanStepStatus.Pending);
}
