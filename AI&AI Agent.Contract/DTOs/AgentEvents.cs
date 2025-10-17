namespace AI_AI_Agent.Contract.DTOs;

public abstract record AgentEvent(string EventType, Guid ChatId)
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}

public record StepThoughtEvent(Guid ChatId, string Thought)
    : AgentEvent(nameof(StepThoughtEvent), ChatId);

public record ToolStartEvent(Guid ChatId, string ToolName, IReadOnlyDictionary<string, object> Arguments)
    : AgentEvent(nameof(ToolStartEvent), ChatId);

public record ToolEndEvent(Guid ChatId, string ToolName, object Result, bool IsError = false)
    : AgentEvent(nameof(ToolEndEvent), ChatId);

public record FinalAnswerEvent(Guid ChatId, string Answer)
    : AgentEvent(nameof(FinalAnswerEvent), ChatId);

public record AgentErrorEvent(Guid ChatId, string ErrorMessage)
    : AgentEvent(nameof(AgentErrorEvent), ChatId);
