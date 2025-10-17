using System.Text.Json;
using AI_AI_Agent.Contracts;

namespace AI_AI_Agent.Application.Agent;

public static class ToolCallParser
{
    // Try to extract the *first* function call from a backend-agnostic result
    public static ToolCallResult? TryParse(IChatResult result)
    {
        if (result.FunctionCalls is { Count: > 0 })
        {
            var fc = result.FunctionCalls[0];
            // Clone the arguments to detach from any underlying JsonDocument lifetime
            var detachedArgs = fc.Arguments.ValueKind == JsonValueKind.Undefined
                ? default
                : fc.Arguments.Clone();
            return new ToolCallResult(fc.Name, detachedArgs);
        }

        // Fallback: try to sniff inline JSON like {"tool":"name","args":{...}}
        if (!string.IsNullOrWhiteSpace(result.Text))
        {
            var text = result.Text!;
            var idx = text.IndexOf('{');
            if (idx >= 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text.Substring(idx));
                    var root = doc.RootElement;
                    if (root.TryGetProperty("tool", out var tn) &&
                        root.TryGetProperty("args", out var args) &&
                        tn.ValueKind == JsonValueKind.String &&
                        args.ValueKind == JsonValueKind.Object)
                    {
                        // Detach args from this temporary JsonDocument before returning
                        var detachedArgs = args.Clone();
                        return new ToolCallResult(tn.GetString()!, detachedArgs);
                    }
                }
                catch { /* ignore */ }
            }
        }

        return null;
    }
}
