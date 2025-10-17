namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Represents a capability that an agent possesses
    /// </summary>
    public class AgentCapability
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> RequiredTools { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
