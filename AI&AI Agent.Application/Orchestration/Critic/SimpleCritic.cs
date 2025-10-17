using AI_AI_Agent.Domain;

namespace AI_AI_Agent.Application.Critic;

public sealed class SimpleCritic : ICritic
{
    public Task<bool> PassAsync(Step step, object? payload, CancellationToken ct)
    {
        if (payload is null) return Task.FromResult(false);
        if (payload is string s) return Task.FromResult(s.Length > 20);
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        return Task.FromResult(json.Length > 20);
    }
}
