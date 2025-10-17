using System.Collections.Concurrent;
using AI.Agent.Domain.Events;

namespace AI.Agent.Application.Budgets;

public sealed class BudgetManager : IBudgetManager
{
    private readonly IEventBus _bus;
    private readonly int _runTokenBudget;
    private int _spent;
    private readonly ConcurrentDictionary<(Guid runId, string stepId), CancellationTokenSource> _cts = new();

    public BudgetManager(IEventBus bus, int runTokenBudget = 100_000)
    {
        _bus = bus;
        _runTokenBudget = runTokenBudget;
    }

    public IDisposable Step(Guid runId, string stepId, TimeSpan? timeout = null, int? tokenBudget = null)
    {
        var cts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
        _cts[(runId, stepId)] = cts;
        return new Scope(this, runId, stepId);
    }

    public bool SpendTokens(int count)
    {
        var after = Interlocked.Add(ref _spent, count);
        return after <= _runTokenBudget;
    }

    private sealed class Scope : IDisposable
    {
        private readonly BudgetManager _owner;
        private readonly Guid _runId;
        private readonly string _stepId;
        private bool _disposed;

        public Scope(BudgetManager owner, Guid runId, string stepId)
        {
            _owner = owner;
            _runId = runId;
            _stepId = stepId;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner._cts.TryRemove((_runId, _stepId), out var cts);
            cts?.Dispose();
        }
    }

    public ValueTask DisposeAsync()
    {
        foreach (var kvp in _cts)
        {
            kvp.Value.Cancel();
            kvp.Value.Dispose();
        }
        _cts.Clear();
        return ValueTask.CompletedTask;
    }
}
