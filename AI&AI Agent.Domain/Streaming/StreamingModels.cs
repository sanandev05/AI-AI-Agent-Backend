namespace AI_AI_Agent.Domain.Streaming
{
    /// <summary>
    /// Streaming message chunk for real-time updates
    /// </summary>
    public class StreamChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; } = string.Empty;
        public StreamChunkType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public enum StreamChunkType
    {
        Text,
        ToolExecution,
        ReasoningTrace,
        ProgressUpdate,
        TypingIndicator,
        Complete,
        Error
    }

    /// <summary>
    /// Tool execution progress for visualization
    /// </summary>
    public class ToolExecutionProgress
    {
        public string ToolName { get; set; } = string.Empty;
        public string Status { get; set; } = "pending"; // pending, running, completed, failed
        public Dictionary<string, object> Arguments { get; set; } = new();
        public object? Result { get; set; }
        public string? Error { get; set; }
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public double Progress { get; set; } = 0; // 0-1
    }

    /// <summary>
    /// Reasoning trace for display
    /// </summary>
    public class ReasoningTraceDisplay
    {
        public List<string> Steps { get; set; } = new();
        public string CurrentThought { get; set; } = string.Empty;
        public List<string> Considerations { get; set; } = new();
        public string Decision { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0;
    }

    /// <summary>
    /// Conversation branch for conversation management
    /// </summary>
    public class ConversationBranch
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ParentMessageId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> MessageIds { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Media attachment for rich media support
    /// </summary>
    public class MediaAttachment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public string StoragePath { get; set; } = string.Empty;
        public string ThumbnailPath { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Conversation export format
    /// </summary>
    public class ConversationExport
    {
        public string ConversationId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
        public List<ExportedMessage> Messages { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class ExportedMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public List<MediaAttachment> Attachments { get; set; } = new();
    }
}
