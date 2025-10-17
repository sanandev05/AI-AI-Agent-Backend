using System.Collections.Concurrent;
using System.Text.Json;
using System.Linq;
using AI.Agent.Domain.Memory;

namespace AI.Agent.Infrastructure.Memory;

public sealed class FileMemoryStore : IMemoryStore
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, MemoryEntry> _items = new(StringComparer.OrdinalIgnoreCase);

    public FileMemoryStore(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        Load();
    }

    public Task<string> AddAsync(MemoryEntry entry, CancellationToken ct = default)
    {
        _items[entry.Id] = entry;
        Persist();
        return Task.FromResult(entry.Id);
    }

    public Task<IReadOnlyList<MemoryEntry>> SearchAsync(float[] vector, int topK = 5, CancellationToken ct = default)
    {
        // Cosine similarity over all items
        var scores = new List<(MemoryEntry e, double score)>();
        foreach (var e in _items.Values)
        {
            var score = CosineSimilarity(vector, e.Vector);
            scores.Add((e, score));
        }
        var results = scores
            .OrderByDescending(s => s.score)
            .Take(topK)
            .Select(s => s.e)
            .ToList();
        return Task.FromResult<IReadOnlyList<MemoryEntry>>(results);
    }

    private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
    {
        if (a.Count != b.Count || a.Count == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Count; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        if (na == 0 || nb == 0) return 0;
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var json = File.ReadAllText(_path);
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json);
            if (entries is null) return;
            foreach (var e in entries) _items[e.Id] = e;
        }
        catch { /* ignore */ }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_items.Values);
            File.WriteAllText(_path, json);
        }
        catch { /* ignore */ }
    }
}
