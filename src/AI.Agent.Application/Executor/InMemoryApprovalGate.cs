using System.Collections.Concurrent;

namespace AI.Agent.Application.Executor;

public sealed class InMemoryApprovalGate : IApprovalGate
{
    private readonly ConcurrentDictionary<(Guid runId, string stepId), TaskCompletionSource<bool>> _waiters = new();

    public Task<bool> WaitAsync(Guid runId, string stepId, CancellationToken ct)
    {
        var key = (runId, stepId);
        var tcs = _waiters.GetOrAdd(key, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
        ct.Register(() => tcs.TrySetCanceled(ct));
        return tcs.Task;
    }

    public void Grant(Guid runId, string stepId)
    {
        var key = (runId, stepId);
        if (_waiters.TryRemove(key, out var tcs)) tcs.TrySetResult(true);
        else _waiters[key] = new(TaskCreationOptions.RunContinuationsAsynchronously) { }; // default granted next time
    }

    public void Deny(Guid runId, string stepId, string reason)
    {
        var key = (runId, stepId);
        if (_waiters.TryRemove(key, out var tcs)) tcs.TrySetResult(false);
        else _waiters[key] = new(TaskCreationOptions.RunContinuationsAsynchronously) { };
    }
}
