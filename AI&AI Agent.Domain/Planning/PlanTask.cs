using AI_AI_Agent.Domain.Agents;

namespace AI_AI_Agent.Domain.Planning
{
    /// <summary>
    /// Represents a task in the execution plan
    /// </summary>
    public class PlanTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = string.Empty;
        public AgentType RequiredAgentType { get; set; }
        public string? SpecificAgentId { get; set; }
        public PlanTaskStatus Status { get; set; } = PlanTaskStatus.Pending;
        public List<string> Dependencies { get; set; } = new(); // IDs of tasks that must complete first
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string? Result { get; set; }
        public string? Error { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int RetryCount { get; set; } = 0;
        public int MaxRetries { get; set; } = 3;
        public double ConfidenceScore { get; set; } = 1.0; // 0.0 to 1.0
        public int Priority { get; set; } = 5; // 1 (highest) to 10 (lowest)
    }

    public enum PlanTaskStatus
    {
        Pending,
        Ready,      // Dependencies satisfied, ready to execute
        Running,
        Completed,
        Failed,
        Skipped,
        Blocked     // Waiting for dependencies
    }
}
