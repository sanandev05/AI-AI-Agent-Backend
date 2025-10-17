using System.Text.Json;
using AI.Agent.Domain.Events;
using Microsoft.SemanticKernel;

namespace AI.Agent.Application.Tools;

public sealed class LlmAnswerTool : ITool
{
    public string Name => "LLM.Answer";

    private readonly Kernel _kernel;

    public LlmAnswerTool(Kernel kernel)
    {
        _kernel = kernel;
    }

    // input schema: { question: string, format?: "plain"|"bullet"|"code" }
    public async Task<(object? payload, IList<Artifact> artifacts, string summary)> RunAsync(JsonElement input, IDictionary<string, object?> ctx, CancellationToken ct)
    {
        var question = input.TryGetProperty("question", out var q) ? q.GetString() : null;
        var format = input.TryGetProperty("format", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : "plain";
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("question is required");

        var narration = new List<string>
        {
            "🧠 LLM-only answering mode",
            $"❓ Question: {question}",
            $"📝 Format: {format}"
        };

        try
        {
            // Heuristic: very simple arithmetic without web
            var simple = TrySimpleMath(question!);
            if (simple is not null)
            {
                var payloadQuick = new { answer = simple, format, question, narration = narration.Append("✅ Answered via local arithmetic heuristic") };
                return (payloadQuick, new List<Artifact>(), $"Answered directly: {simple}");
            }

            var style = format switch
            {
                "bullet" => "Answer in concise bullet points.",
                "code" => "If applicable, include a minimal code block.",
                _ => "Answer plainly and directly."
            };

            var prompt = $@"You are a precise assistant. {style}
Return STRICT JSON with fields: answer (string), confidence (0..1 number), justification (short string).

Question:
{question}
";

            var result = await _kernel.InvokePromptAsync(prompt, cancellationToken: ct);
            var output = result?.ToString() ?? string.Empty;

            double confidence = 0.5;
            string answer = output.Trim();
            string justification = string.Empty;
            try
            {
                using var doc = JsonDocument.Parse(output);
                var root = doc.RootElement;
                answer = root.TryGetProperty("answer", out var a) ? a.GetString() ?? answer : answer;
                if (root.TryGetProperty("confidence", out var c) && c.ValueKind == JsonValueKind.Number) confidence = c.GetDouble();
                justification = root.TryGetProperty("justification", out var j) ? j.GetString() ?? string.Empty : string.Empty;
            }
            catch { /* fall back to raw text */ }

            var payload = new { answer, confidence, justification, format, question, narration };
            var summary = answer.Length > 0 ? $"LLM produced {answer.Length} chars (conf {confidence:0.00})" : "LLM produced empty answer";
            return (payload, new List<Artifact>(), summary);
        }
        catch (Exception ex)
        {
            narration.Add($"❌ LLM error: {ex.Message}");
            // Fallback: echo question; caller can still see failure narration
            var payload = new { answer = (string?)null, format, question, error = ex.Message, narration };
            return (payload, new List<Artifact>(), "LLM.Answer failed; see narration");
        }
    }

    private static string? TrySimpleMath(string input)
    {
        try
        {
            // Very small evaluator for expressions like 5+3, 10 - 2*3, (2+3)*4
            var expr = new System.Data.DataTable().Compute(input, "");
            return Convert.ToString(expr);
        }
        catch { return null; }
    }
}
