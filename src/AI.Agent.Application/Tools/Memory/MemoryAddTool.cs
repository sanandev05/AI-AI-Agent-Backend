using System.Text.Json;
using AI.Agent.Domain.Memory;

namespace AI.Agent.Application.Tools.Memory;

public sealed class MemoryAddTool : ITool
{
    public string Name => "Memory.Add";
    private readonly IMemoryStore _store;
    private readonly Func<string, CancellationToken, Task<float[]>> _embed;

    public MemoryAddTool(IMemoryStore store, Func<string, CancellationToken, Task<float[]>> embed)
    {
        _store = store;
        _embed = embed;
    }

    public async Task<(object? payload, IList<AI.Agent.Domain.Events.Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var text = input.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
        var source = input.TryGetProperty("source", out var s) ? s.GetString() : null;
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (input.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in tagsEl.EnumerateObject()) tags[p.Name] = p.Value.GetString() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return (new { ok = false, error = "text is required" }, new List<AI.Agent.Domain.Events.Artifact>(), "No text provided");
        }
        var vec = await _embed(text, ct);
        var id = Guid.NewGuid().ToString("N");
        var entry = new MemoryEntry(id, text, vec, DateTimeOffset.UtcNow, source, tags);
        await _store.AddAsync(entry, ct);
        return (new { ok = true, id }, new List<AI.Agent.Domain.Events.Artifact>(), "Memory item added");
    }
}
