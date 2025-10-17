using System.Collections.Concurrent;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application;

/// <summary>
/// An approval gate that can optionally block on steps until a Grant/Deny signal arrives.
/// By default, RequiresApproval returns false to preserve current behavior. You can change
/// the policy to require approvals for risky tools. Hubs/Controllers can call Grant/Deny
/// via the IApprovalCoordinator interface to unblock waiting steps.
/// </summary>
public sealed class InMemoryApprovalGate : IApprovalGate, IApprovalCoordinator
{
    private readonly ConcurrentDictionary<(Guid runId, string stepId), TaskCompletionSource<bool>> _waiters = new();

    // Tool names that should require approval; empty means none by default.
    private static readonly HashSet<string> RiskyTools = new(StringComparer.OrdinalIgnoreCase)
    {
        // e.g., "Browser.Click", "Browser.Type", "Browser.Submit", "Browser.Goto", "Docx.Create"
    };

    public bool RequiresApproval(string toolName)
    {
        return RiskyTools.Contains(toolName);
    }

    public Task<bool> WaitForApprovalAsync(Guid runId, string stepId, string toolName, object? input, CancellationToken ct)
    {
        var key = (runId, stepId);
        var tcs = _waiters.GetOrAdd(key, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        // If the caller cancels, try to remove waiter and cancel the task
        if (ct.CanBeCanceled)
        {
            ct.Register(() =>
            {
                if (_waiters.TryRemove(key, out var pending))
                {
                    pending.TrySetCanceled(ct);
                }
            });
        }
        return tcs.Task;
    }

    public Task GrantAsync(Guid runId, string stepId)
    {
        var key = (runId, stepId);
        if (_waiters.TryRemove(key, out var tcs))
        {
            tcs.TrySetResult(true);
        }
        return Task.CompletedTask;
    }

    public Task DenyAsync(Guid runId, string stepId, string reason)
    {
        var key = (runId, stepId);
        if (_waiters.TryRemove(key, out var tcs))
        {
            tcs.TrySetResult(false);
        }
        return Task.CompletedTask;
    }
}
