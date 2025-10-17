using AI_AI_Agent.Domain;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application;

public interface IPlanner { Task<Plan> MakePlanAsync(string goal, CancellationToken ct); }
public interface IExecutor { Task ExecuteAsync(Guid runId, Plan plan, CancellationToken ct); }
public interface ICritic { Task<bool> PassAsync(Step step, object? payload, CancellationToken ct); }
public interface IBudgetManager : IAsyncDisposable {
    IDisposable Step(Guid runId, string stepId, TimeSpan? timeout=null, int? tokenBudget=null);
    bool SpendTokens(int count);
}
public interface ITool {
    string Name { get; }
    Task<(object? payload, IList<Artifact> artifacts, string summary)>
       RunAsync(System.Text.Json.JsonElement input, IDictionary<string,object?> ctx, CancellationToken ct);
}
public interface IToolRouter {
    Task<(object? payload, IList<Artifact> artifacts, string summary)>
       ExecuteAsync(string tool, System.Text.Json.JsonElement input, IDictionary<string,object?> ctx, CancellationToken ct);
    // Expose known tool names for planner validation and diagnostics
    IReadOnlyCollection<string> Names();
}
public interface IEventBus { Task PublishAsync(object evt, CancellationToken ct=default); }
public interface IArtifactStore { Task<Artifact> SaveAsync(Guid runId, string stepId, string localPath, string? fileName=null, string? mime=null); }
public interface IRunStore {
    Task MarkStepAsync(Guid runId, string stepId, StepState state);
    Task<(DateTime started, DateTime? ended)> MarkRunStartAsync(Guid runId);
    Task MarkRunEndAsync(Guid runId, DateTime ended);
}

// Approval service allows the executor to pause risky steps until approval.
public interface IApprovalGate
{
    // Returns true if the tool requires explicit user approval
    bool RequiresApproval(string toolName);
    // Wait for approval for a given run/step; implementation may auto-approve, block for SignalR, etc.
    Task<bool> WaitForApprovalAsync(Guid runId, string stepId, string toolName, object? input, CancellationToken ct);
}

// Coordinator API for hubs/controllers to grant/deny approvals at runtime.
public interface IApprovalCoordinator
{
    Task GrantAsync(Guid runId, string stepId);
    Task DenyAsync(Guid runId, string stepId, string reason);
}
