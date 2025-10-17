namespace AI_AI_Agent.Domain.Observability
{
    /// <summary>
    /// Represents a trace of agent execution
    /// </summary>
    public class ExecutionTrace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
        public string AgentId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public ExecutionStatus Status { get; set; } = ExecutionStatus.InProgress;
        public Dictionary<string, string> Tags { get; set; } = new();
        public List<ExecutionSpan> Spans { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<string> Logs { get; set; } = new();
        public string? Error { get; set; }
    }

    /// <summary>
    /// Represents a span within an execution trace
    /// </summary>
    public class ExecutionSpan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ParentId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;
        public Dictionary<string, string> Attributes { get; set; } = new();
        public List<string> Events { get; set; } = new();
    }

    public enum ExecutionStatus
    {
        InProgress,
        Completed,
        Failed,
        Cancelled
    }
}
