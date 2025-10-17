using System.Collections.Concurrent;

namespace AI_AI_Agent.Application.Agent.Planning;

public interface IPlanStore
{
    Task<Plan?> GetAsync(string chatId, CancellationToken ct);
    Task SaveAsync(Plan plan, CancellationToken ct);
}

public class InMemoryPlanStore : IPlanStore
{
    private readonly ConcurrentDictionary<string, Plan> _plans = new();

    public Task<Plan?> GetAsync(string chatId, CancellationToken ct) =>
        Task.FromResult(_plans.TryGetValue(chatId, out var plan) ? plan : null);

    public Task SaveAsync(Plan plan, CancellationToken ct)
    {
        _plans[plan.ChatId] = plan;
        return Task.CompletedTask;
    }
}
