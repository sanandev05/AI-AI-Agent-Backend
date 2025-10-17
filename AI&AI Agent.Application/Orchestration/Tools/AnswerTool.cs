using System.Text;
using AI_AI_Agent.Domain.Events;
using Microsoft.SemanticKernel;

namespace AI_AI_Agent.Application.Tools;

/// <summary>
/// Answers the user's prompt using the configured LLM via Semantic Kernel without using external tools.
/// Useful when the task doesn't require fresh data or site-specific retrieval.
/// </summary>
public sealed class AnswerTool : ITool
{
    public string Name => "LLM.Answer";

    private readonly Kernel _kernel;
    public AnswerTool(Kernel kernel)
    {
        _kernel = kernel;
    }

    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(System.Text.Json.JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var prompt = input.TryGetProperty("prompt", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.String ? (p.GetString() ?? string.Empty) : string.Empty;
        var minWords = input.TryGetProperty("minWords", out var mw) && mw.ValueKind == System.Text.Json.JsonValueKind.Number ? Math.Max(0, mw.GetInt32()) : 120;
        var style = input.TryGetProperty("style", out var st) && st.ValueKind == System.Text.Json.JsonValueKind.String ? (st.GetString() ?? "concise") : "concise";

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("prompt is required");
        }

        var system = style.Equals("concise", StringComparison.OrdinalIgnoreCase)
            ? "You are a helpful, factual assistant. Answer clearly and precisely."
            : "You are a helpful assistant. Provide a thorough and accurate answer.";

        var sb = new StringBuilder();
        sb.AppendLine(system);
        sb.AppendLine();
        sb.AppendLine("User question:");
        sb.AppendLine(prompt);
        sb.AppendLine();
        if (minWords > 0)
        {
            sb.AppendLine($"Write at least {minWords} words if the topic warrants it.");
        }

        try
        {
            var response = await _kernel.InvokePromptAsync(sb.ToString(), cancellationToken: ct);
            var text = response?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                text = "I'm not confident in an answer without external sources.";
            }
            var summaryOk = "Local LLM answer produced (no external tools).";
            return (text, Array.Empty<Artifact>(), summaryOk);
        }
        catch
        {
            // Safe fallback: try simple arithmetic; otherwise provide a concise echo
            string text;
            if (TrySimpleMath(prompt, out var result))
            {
                text = $"Answer: {result}";
            }
            else
            {
                text = $"I couldn't complete an LLM answer right now. Your question was: {prompt}";
            }
            var summaryFail = "Local fallback answer provided (LLM unavailable).";
            return (text, Array.Empty<Artifact>(), summaryFail);
        }
    }

    private static bool TrySimpleMath(string s, out double value)
    {
        // Very small evaluator for expressions like "5 + 3", "12-7", "6*4", "8 / 2"
        value = 0;
        try
        {
            var expr = s.Replace("×", "*").Replace("÷", "/");
            // Extract numbers and operator
            var ops = new[] { '+', '-', '*', '/' };
            foreach (var op in ops)
            {
                var idx = expr.IndexOf(op);
                if (idx > 0 && idx < expr.Length - 1)
                {
                    var left = expr.Substring(0, idx).Trim();
                    var right = expr.Substring(idx + 1).Trim();
                    if (double.TryParse(left, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var a)
                        && double.TryParse(right, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var b))
                    {
                        value = op switch
                        {
                            '+' => a + b,
                            '-' => a - b,
                            '*' => a * b,
                            '/' => b == 0 ? double.NaN : a / b,
                            _ => 0
                        };
                        return true;
                    }
                }
            }
            return false;
        }
        catch { return false; }
    }
}
