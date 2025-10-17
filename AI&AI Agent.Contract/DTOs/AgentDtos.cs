using System.Text.Json.Serialization;

namespace AI_AI_Agent.Contract.DTOs;

public record ToolCallRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("functionName")] string FunctionName,
    [property: JsonPropertyName("pluginName")] string PluginName,
    [property: JsonPropertyName("arguments")] IReadOnlyDictionary<string, object> Arguments
);

public record ToolCallResult(
    [property: JsonPropertyName("requestId")] string RequestId,
    [property: JsonPropertyName("result")] object Result,
    [property: JsonPropertyName("isError")] bool IsError = false,
    [property: JsonPropertyName("errorReason")] string? ErrorReason = null
);

public record TokenUsage(
    [property: JsonPropertyName("promptTokens")] int PromptTokens,
    [property: JsonPropertyName("completionTokens")] int CompletionTokens
)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
}
