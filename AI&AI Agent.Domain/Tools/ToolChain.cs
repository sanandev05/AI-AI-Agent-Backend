namespace AI_AI_Agent.Domain.Tools
{
    /// <summary>
    /// Represents a single step in a tool chain
    /// </summary>
    public class ToolChainStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ToolName { get; set; } = string.Empty;
        public Dictionary<string, object> Arguments { get; set; } = new();
        public string? OutputVariable { get; set; }
        public List<string> DependsOn { get; set; } = new(); // Step IDs this depends on
        public ToolChainStepStatus Status { get; set; } = ToolChainStepStatus.Pending;
        public object? Result { get; set; }
        public string? Error { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
    }

    public enum ToolChainStepStatus
    {
        Pending,
        Ready,
        Running,
        Completed,
        Failed,
        Skipped
    }

    /// <summary>
    /// Represents a complete tool execution chain
    /// </summary>
    public class ToolChain
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public List<ToolChainStep> Steps { get; set; } = new();
        public Dictionary<string, object> Variables { get; set; } = new();
        public ToolChainStatus Status { get; set; } = ToolChainStatus.Created;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<string> ExecutionLog { get; set; } = new();
    }

    public enum ToolChainStatus
    {
        Created,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
