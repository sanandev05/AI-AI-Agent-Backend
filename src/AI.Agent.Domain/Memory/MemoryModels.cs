namespace AI.Agent.Domain.Memory;

public record MemoryEntry(
    string Id,
    string Text,
    float[] Vector,
    DateTimeOffset Timestamp,
    string? Source = null,
    IDictionary<string, string>? Tags = null
);

public interface IMemoryStore
{
    Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<MemoryEntry>> SearchAsync(float[] vector, int topK = 5, CancellationToken ct = default);
}
