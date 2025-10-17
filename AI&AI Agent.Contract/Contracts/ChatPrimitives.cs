using System.Text.Json;

namespace AI_AI_Agent.Contracts;

// Simple role/content pair for chat history
public sealed record ChatMessageContent(string Role, string Content);

// A function/tool call extracted from the model output
public sealed record FunctionCall(string Name, JsonElement Arguments);

// What the parser returns after resolving a tool call
public sealed record ToolCallResult(string Name, JsonElement Arguments);

// A minimal result surface used by backends and the parser
public interface IChatResult
{
    // Raw assistant text (when no function call is made)
    string? Text { get; }

    // Zero or more function calls returned by the model
    IReadOnlyList<FunctionCall> FunctionCalls { get; }
}
