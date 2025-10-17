using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent;

// Unified tool contract used by the AgentLoop and tool implementations
public interface ITool
{
    // Unique tool name used to route calls
    string Name { get; }

    // Invoke the tool with model-provided JSON arguments
    Task<object> InvokeAsync(JsonElement args, CancellationToken cancellationToken);
}
