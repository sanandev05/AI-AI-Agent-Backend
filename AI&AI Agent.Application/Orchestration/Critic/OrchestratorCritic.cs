using AI_AI_Agent.Domain;

namespace AI_AI_Agent.Application.Critic;

public sealed class OrchestratorCritic : ICritic
{
    public Task<bool> PassAsync(Step step, object? payload, CancellationToken ct)
    {
        if (payload is null) return Task.FromResult(false);

        // Basic size check
        string text = payload switch
        {
            string s => s,
            _ => System.Text.Json.JsonSerializer.Serialize(payload)
        };
        // Allow Browser.Search to pass even with 0 candidates (executor may handle via fallback)
        if (!step.Tool.Equals("Browser.Search", System.StringComparison.OrdinalIgnoreCase) && text.Length < 40)
            return Task.FromResult(false);

        // Tool-specific heuristics
        var tool = step.Tool;
        if (tool.Equals("Summarize", System.StringComparison.OrdinalIgnoreCase))
        {
            // If the summarize mode is final-synthesis, require citations
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(step.Input.GetRawText());
                var root = doc.RootElement;
                if (root.TryGetProperty("mode", out var m) && m.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var mode = m.GetString();
                    if (string.Equals(mode, "final-synthesis", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // must contain a Citations section or at least 2 URLs
                        var urlCount = System.Text.RegularExpressions.Regex.Matches(text, @"https?://[\w\-./?%&=#]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
                        if (urlCount < 2 && !text.Contains("Citations:", System.StringComparison.OrdinalIgnoreCase))
                            return Task.FromResult(false);
                    }
                }
            }
            catch { }
        }
        else if (tool.Equals("Browser.Extract", System.StringComparison.OrdinalIgnoreCase))
        {
            // Ensure we have non-thin content
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("thin", out var thin) && thin.ValueKind == System.Text.Json.JsonValueKind.True)
                    return Task.FromResult(false);
            }
            catch { }
        }
        return Task.FromResult(true);
    }
}
