using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Application.Routing;

public sealed class ToolRouter : IToolRouter
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;
    public ToolRouter(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }
    public Task<(object? payload, IList<Artifact> artifacts, string summary)> ExecuteAsync(string tool, System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        if (!_tools.TryGetValue(tool, out var t)) throw new InvalidOperationException($"Unknown tool: {tool}");
        return t.RunAsync(input, ctx, ct);
    }

    public IReadOnlyCollection<string> Names()
    {
        return _tools.Keys.ToArray();
    }
}
