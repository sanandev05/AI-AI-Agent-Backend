using System;
using System.Collections.Generic;
using System.Linq;

namespace AI_AI_Agent.Application.Agent.Routing;

public class LLMRouter
{
    private readonly IReadOnlyDictionary<string, IChatBackend> _backends;

    public LLMRouter(IEnumerable<IChatBackend> backends)
    {
        _backends = backends.ToDictionary(b => b.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IChatBackend GetBackend(string prompt, int historyLength)
    {
        if (_backends.Count == 0)
        {
            throw new InvalidOperationException("No chat backends registered. Configure Agent:Backends or OpenAI:ApiKey.");
        }
        // Simple heuristic-based routing
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase) || prompt.Contains("function", StringComparison.OrdinalIgnoreCase))
        {
            return _backends.GetValueOrDefault("Code", _backends.First().Value);
        }

        if (prompt.Contains("browse", StringComparison.OrdinalIgnoreCase) || prompt.Contains("search", StringComparison.OrdinalIgnoreCase) || prompt.Contains("analyze", StringComparison.OrdinalIgnoreCase))
        {
            return _backends.GetValueOrDefault("Reasoning", _backends.First().Value);
        }

        if (historyLength > 10)
        {
            return _backends.GetValueOrDefault("LongContext", _backends.First().Value);
        }

        return _backends.GetValueOrDefault("Cheap", _backends.First().Value);
    }
}
