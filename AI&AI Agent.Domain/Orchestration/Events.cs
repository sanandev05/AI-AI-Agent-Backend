namespace AI_AI_Agent.Domain.Events;

public record RunStarted(Guid RunId, string Goal);
public record PlanCreated(Guid RunId, string Goal, IReadOnlyList<Step> Steps);
public record StepStarted(Guid RunId, string StepId, string Tool, object? Input);
public record ToolOutput(Guid RunId, string StepId, string Summary);
public record Artifact(string FileName, string Path, string MimeType, long Size);
public record ArtifactCreated(Guid RunId, string StepId, Artifact Artifact);
public record StepSucceeded(Guid RunId, string StepId);
public record StepFailed(Guid RunId, string StepId, string Error, int Attempt);
public record RunSucceeded(Guid RunId, TimeSpan Elapsed);
public record RunFailed(Guid RunId, string Error);
public record BudgetExceeded(Guid RunId, string What, string Details);

// Approval workflow events for risky actions
public record PermissionRequested(Guid RunId, string StepId, string Tool, object? Input);
public record PermissionGranted(Guid RunId, string StepId);
public record PermissionDenied(Guid RunId, string StepId, string Reason);
