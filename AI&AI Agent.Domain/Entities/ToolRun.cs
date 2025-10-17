namespace AI_AI_Agent.Domain.Entities;

public class ToolRun : BaseEntity
{
    public Guid MessageId { get; set; }
    public virtual Message Message { get; set; } = null!;

    public string ToolName { get; set; } = string.Empty;
    public string ToolInput { get; set; } = string.Empty; // JSON of arguments
    public string Output { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
}
