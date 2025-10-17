using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.SemanticKernel;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class TranslateTool : ITool
{
    public string Name => "Translate";
    public string Description => "Translate input text to target language using configured LLM.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            text = new { type = "string" },
            targetLanguage = new { type = "string", description = "e.g., Spanish, Turkish, Arabic" }
        },
        required = new[] { "text", "targetLanguage" }
    };

    private readonly Kernel _kernel;
    public TranslateTool(Kernel kernel) { _kernel = kernel; }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        var text = args.GetProperty("text").GetString() ?? string.Empty;
        var lang = args.GetProperty("targetLanguage").GetString() ?? "";
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(lang)) return "Error: text and targetLanguage required.";

        var prompt = @"Translate the following text into {{lang}}. Provide only the translated text, no explanations.

Text:
{{text}}";
        var fn = _kernel.CreateFunctionFromPrompt(prompt);
        var result = await _kernel.InvokeAsync(fn, new() { { "lang", lang }, { "text", text } }, ct);
        var translated = result.GetValue<string>() ?? string.Empty;
        return new { translated, language = lang };
    }
}
