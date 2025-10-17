namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Registry for managing and discovering agents
    /// </summary>
    public interface IAgentRegistry
    {
        /// <summary>
        /// Register a new agent in the system
        /// </summary>
        Task RegisterAgentAsync(AgentMetadata metadata, CancellationToken cancellationToken = default);

        /// <summary>
        /// Unregister an agent from the system
        /// </summary>
        Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get agent metadata by ID
        /// </summary>
        Task<AgentMetadata?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get all registered agents
        /// </summary>
        Task<IReadOnlyList<AgentMetadata>> GetAllAgentsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Find agents by capability
        /// </summary>
        Task<IReadOnlyList<AgentMetadata>> FindAgentsByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Find agents by type
        /// </summary>
        Task<IReadOnlyList<AgentMetadata>> FindAgentsByTypeAsync(AgentType type, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if an agent is registered
        /// </summary>
        Task<bool> IsAgentRegisteredAsync(string agentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Update agent metadata
        /// </summary>
        Task UpdateAgentAsync(AgentMetadata metadata, CancellationToken cancellationToken = default);
    }
}
