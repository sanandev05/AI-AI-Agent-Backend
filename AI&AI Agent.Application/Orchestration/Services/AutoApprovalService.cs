using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application;

// Default approval service: auto-approves everything. Replace with a SignalR-driven one to prompt users.
public sealed class AutoApprovalGate : IApprovalGate
{
    public bool RequiresApproval(string toolName)
    {
        // By default, no tool requires approval; change this policy as needed.
        return false;
    }

    public Task<bool> WaitForApprovalAsync(Guid runId, string stepId, string toolName, object? input, CancellationToken ct)
    {
        // Always approve immediately.
        return Task.FromResult(true);
    }
}
