namespace AI.Agent.Application.Routing;

public sealed class ToolRouter : IToolRouter
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;

    public ToolRouter(IEnumerable<ITool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
    }

    public Task<(object? payload, IList<AI.Agent.Domain.Events.Artifact> artifacts, string summary)> ExecuteAsync(string tool, System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        if (!_tools.TryGetValue(tool, out var t))
        {
            throw new InvalidOperationException($"Unknown tool: {tool}");
        }
        return t.RunAsync(input, ctx, ct);
    }
}
