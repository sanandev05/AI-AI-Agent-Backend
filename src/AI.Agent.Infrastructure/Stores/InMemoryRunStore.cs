using System.Collections.Concurrent;
using AI.Agent.Application;
using AI.Agent.Domain;

namespace AI.Agent.Infrastructure.Stores;

public sealed class InMemoryRunStore : IRunStore
{
    private readonly ConcurrentDictionary<Guid, (DateTime started, DateTime? ended)> _runs = new();
    private readonly ConcurrentDictionary<(Guid runId, string stepId), StepState> _steps = new();

    public Task<(DateTime started, DateTime? ended)> MarkRunStartAsync(Guid runId)
    {
        var state = (DateTime.UtcNow, (DateTime?)null);
        _runs[runId] = state;
        return Task.FromResult(state);
    }

    public Task MarkRunEndAsync(Guid runId, DateTime ended)
    {
        _runs.AddOrUpdate(runId, _ => (DateTime.UtcNow, ended), (_, s) => (s.started, ended));
        return Task.CompletedTask;
    }

    public Task MarkStepAsync(Guid runId, string stepId, StepState state)
    {
        _steps[(runId, stepId)] = state;
        return Task.CompletedTask;
    }
}
