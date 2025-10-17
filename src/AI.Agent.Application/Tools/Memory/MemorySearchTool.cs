using System.Text.Json;
using AI.Agent.Domain.Memory;

namespace AI.Agent.Application.Tools.Memory;

public sealed class MemorySearchTool : ITool
{
    public string Name => "Memory.Search";
    private readonly IMemoryStore _store;
    private readonly Func<string, CancellationToken, Task<float[]>> _embed;

    public MemorySearchTool(IMemoryStore store, Func<string, CancellationToken, Task<float[]>> embed)
    {
        _store = store;
        _embed = embed;
    }

    public async Task<(object? payload, IList<AI.Agent.Domain.Events.Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var query = input.TryGetProperty("query", out var q) ? q.GetString() ?? string.Empty : string.Empty;
        var topK = input.TryGetProperty("topK", out var k) ? k.GetInt32() : 5;
        if (string.IsNullOrWhiteSpace(query))
        {
            return (new { ok = false, error = "query is required" }, new List<AI.Agent.Domain.Events.Artifact>(), "No query provided");
        }
        var vec = await _embed(query, ct);
        var results = await _store.SearchAsync(vec, topK, ct);
        var payload = results.Select(r => new { r.Id, r.Text, r.Source, r.Timestamp, r.Tags }).ToList();
        return (payload, new List<AI.Agent.Domain.Events.Artifact>(), $"{payload.Count} memory results");
    }
}
