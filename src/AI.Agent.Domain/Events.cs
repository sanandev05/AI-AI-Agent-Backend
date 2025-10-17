namespace AI.Agent.Domain.Events;

/// <summary>
/// Event emitted when a run begins.
/// </summary>
public record RunStarted(Guid RunId, string Goal);

/// <summary>
/// Event emitted when a plan is generated for a run.
/// </summary>
public record PlanCreated(Guid RunId, string Goal, IReadOnlyList<Step> Steps);

/// <summary>
/// Event emitted when a step begins execution.
/// </summary>
public record StepStarted(Guid RunId, string StepId, string Tool, object? Input);

/// <summary>
/// Event summarizing tool output.
/// </summary>
public record ToolOutput(Guid RunId, string StepId, string Summary);

/// <summary>
/// File artifact metadata.
/// </summary>
public record Artifact(string FileName, string Path, string MimeType, long Size);

/// <summary>
/// Event emitted after an artifact is created.
/// </summary>
public record ArtifactCreated(Guid RunId, string StepId, Artifact Artifact);

/// <summary>
/// Event emitted when a step succeeds.
/// </summary>
public record StepSucceeded(Guid RunId, string StepId);

/// <summary>
/// Event emitted when a step fails.
/// </summary>
public record StepFailed(Guid RunId, string StepId, string Error, int Attempt);

/// <summary>
/// Event emitted when the run succeeds.
/// </summary>
public record RunSucceeded(Guid RunId, TimeSpan Elapsed);

/// <summary>
/// Event emitted when the run fails.
/// </summary>
public record RunFailed(Guid RunId, string Error);

/// <summary>
/// Event emitted when a time or token budget is exceeded.
/// </summary>
public record BudgetExceeded(Guid RunId, string What, string Details);

/// <summary>
/// Human approval workflow events for risky tool execution.
/// </summary>
public record PermissionRequested(Guid RunId, string StepId, string Tool, object? Input);
public record PermissionGranted(Guid RunId, string StepId);
public record PermissionDenied(Guid RunId, string StepId, string Reason);
