namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Metadata describing an agent's identity and capabilities
    /// </summary>
    public class AgentMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Instructions { get; set; } = string.Empty;
        public AgentType Type { get; set; }
        public List<AgentCapability> Capabilities { get; set; } = new();
        public List<string> AvailableTools { get; set; } = new();
        public Dictionary<string, string> Configuration { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Types of agents in the system
    /// </summary>
    public enum AgentType
    {
        Orchestrator,
        Research,
        Code,
        Files,
        DataAnalysis,
        Custom
    }
}
