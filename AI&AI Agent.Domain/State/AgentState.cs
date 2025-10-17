namespace AI_AI_Agent.Domain.State
{
    /// <summary>
    /// Represents the persisted state of an agent
    /// </summary>
    public class AgentState
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AgentId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public Dictionary<string, object> Data { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public AgentStateStatus Status { get; set; } = AgentStateStatus.Active;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CheckpointedAt { get; set; }
        public string? CheckpointId { get; set; }
        public int Version { get; set; } = 1;
    }

    public enum AgentStateStatus
    {
        Active,
        Suspended,
        Completed,
        Failed,
        Archived
    }
}
