namespace AI_AI_Agent.Domain.Collaboration
{
    /// <summary>
    /// Agent message for inter-agent communication
    /// </summary>
    public class AgentMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromAgentId { get; set; } = string.Empty;
        public string ToAgentId { get; set; } = string.Empty;
        public AgentMessageType Type { get; set; }
        public string Content { get; set; } = string.Empty;
        public Dictionary<string, object> Payload { get; set; } = new();
        public string? InReplyTo { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReceivedAt { get; set; }
        public bool RequiresResponse { get; set; } = false;
    }

    public enum AgentMessageType
    {
        TaskDelegation,
        Query,
        Response,
        Update,
        Notification,
        ConflictResolution
    }

    /// <summary>
    /// Task delegation between agents
    /// </summary>
    public class TaskDelegation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FromAgentId { get; set; } = string.Empty;
        public string ToAgentId { get; set; } = string.Empty;
        public string TaskDescription { get; set; } = string.Empty;
        public Dictionary<string, object> Context { get; set; } = new();
        public string Priority { get; set; } = "medium";
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "pending";
        public object? Result { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    /// <summary>
    /// Collaborative problem-solving session
    /// </summary>
    public class CollaborativeSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Problem { get; set; } = string.Empty;
        public List<string> ParticipantAgentIds { get; set; } = new();
        public List<AgentContribution> Contributions { get; set; } = new();
        public string Status { get; set; } = "active";
        public object? FinalSolution { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }

    public class AgentContribution
    {
        public string AgentId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0;
        public List<string> SupportingEvidence { get; set; } = new();
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Conflict resolution case
    /// </summary>
    public class ConflictCase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public List<string> ConflictingAgentIds { get; set; } = new();
        public string ConflictDescription { get; set; } = string.Empty;
        public List<ConflictPosition> Positions { get; set; } = new();
        public ConflictResolutionStrategy Strategy { get; set; }
        public string Resolution { get; set; } = string.Empty;
        public string ResolvedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }
    }

    public class ConflictPosition
    {
        public string AgentId { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0;
        public List<string> Arguments { get; set; } = new();
    }

    public enum ConflictResolutionStrategy
    {
        Voting,
        HighestConfidence,
        Consensus,
        HumanArbitration,
        EvidenceBased
    }

    /// <summary>
    /// Agent team for complex tasks
    /// </summary>
    public class AgentTeam
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string LeaderAgentId { get; set; } = string.Empty;
        public List<TeamMember> Members { get; set; } = new();
        public string Goal { get; set; } = string.Empty;
        public TeamStatus Status { get; set; } = TeamStatus.Forming;
        public List<string> CompletedTasks { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class TeamMember
    {
        public string AgentId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> Responsibilities { get; set; } = new();
        public bool IsActive { get; set; } = true;
    }

    public enum TeamStatus
    {
        Forming,
        Active,
        Paused,
        Completed,
        Disbanded
    }

    /// <summary>
    /// Parallel execution plan for teams
    /// </summary>
    public class ParallelExecutionPlan
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TeamId { get; set; } = string.Empty;
        public List<ParallelTask> Tasks { get; set; } = new();
        public string Status { get; set; } = "pending";
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ParallelTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AssignedAgentId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public object? Result { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
