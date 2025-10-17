using AI_AI_Agent.Domain.Planning;

namespace AI_AI_Agent.Domain.Planning
{
    /// <summary>
    /// Represents a complete execution plan with task dependencies
    /// </summary>
    public class ExecutionPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Goal { get; set; } = string.Empty;
        public List<PlanTask> Tasks { get; set; } = new();
        public ExecutionPlanStatus Status { get; set; } = ExecutionPlanStatus.Created;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int TotalTasks => Tasks.Count;
        public int CompletedTasks => Tasks.Count(t => t.Status == PlanTaskStatus.Completed);
        public int FailedTasks => Tasks.Count(t => t.Status == PlanTaskStatus.Failed);
        public double Progress => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks : 0;
        public Dictionary<string, string> Context { get; set; } = new(); // Shared context between tasks
        public string? FinalResult { get; set; }
        public List<string> ExecutionLog { get; set; } = new();
    }

    public enum ExecutionPlanStatus
    {
        Created,
        Running,
        Paused,
        Completed,
        Failed,
        Cancelled
    }
}
