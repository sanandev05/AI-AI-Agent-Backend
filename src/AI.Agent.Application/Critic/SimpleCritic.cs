namespace AI.Agent.Application.Critic;

public sealed class SimpleCritic : ICritic
{
    public Task<bool> PassAsync(AI.Agent.Domain.Step step, object? payload, CancellationToken ct)
    {
        if (payload is null) return Task.FromResult(false);

        if (string.Equals(step.Tool, "LLM.Answer", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var conf = root.TryGetProperty("confidence", out var c) && c.ValueKind == System.Text.Json.JsonValueKind.Number ? c.GetDouble() : 0.0;
                var ans = root.TryGetProperty("answer", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                // Suspicious patterns (e.g., aircraft codes F-###, years, large figures) may require citations
                var suspicious = System.Text.RegularExpressions.Regex.IsMatch(ans, @"\bF-\d{3,4}\b") || System.Text.RegularExpressions.Regex.IsMatch(ans, @"\b(19|20)\d{2}\b");
                return Task.FromResult(conf >= 0.6 && !suspicious);
            }
            catch { return Task.FromResult(false); }
        }

        // Tool-specific checks
        if (string.Equals(step.Tool, "Browser.Extract", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                var text = root.TryGetProperty("text", out var t) ? t.GetString() ?? string.Empty : string.Empty;
                if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(false);
                var thin = text.Length < 800;
                var bad = text.Contains("404", StringComparison.OrdinalIgnoreCase) || text.Contains("page not found", StringComparison.OrdinalIgnoreCase);
                return Task.FromResult(!(thin || bad));
            }
            catch { return Task.FromResult(false); }
        }

        if (string.Equals(step.Tool, "Summarize", StringComparison.OrdinalIgnoreCase))
        {
            // If final synthesis, ensure it's not trivially short
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var s = payload as string ?? json;
                var words = (s ?? string.Empty).Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
                return Task.FromResult(words >= 200);
            }
            catch { return Task.FromResult(false); }
        }

        // Generic default check
        if (payload is string s2) return Task.FromResult(s2.Length > 20);
        var j = System.Text.Json.JsonSerializer.Serialize(payload);
        return Task.FromResult(j.Length > 20);
    }
}
