using AI_AI_Agent.Domain.Agents;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Agents
{
    /// <summary>
    /// In-memory implementation of agent registry
    /// Can be extended to use database storage in the future
    /// </summary>
    public class AgentRegistry : IAgentRegistry
    {
        private readonly ConcurrentDictionary<string, AgentMetadata> _agents = new();

        public Task RegisterAgentAsync(AgentMetadata metadata, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Id, nameof(metadata.Id));

            metadata.CreatedAt = DateTime.UtcNow;
            metadata.UpdatedAt = DateTime.UtcNow;

            if (!_agents.TryAdd(metadata.Id, metadata))
            {
                throw new InvalidOperationException($"Agent with ID '{metadata.Id}' is already registered");
            }

            return Task.CompletedTask;
        }

        public Task UnregisterAgentAsync(string agentId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

            _agents.TryRemove(agentId, out _);
            return Task.CompletedTask;
        }

        public Task<AgentMetadata?> GetAgentAsync(string agentId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

            _agents.TryGetValue(agentId, out var metadata);
            return Task.FromResult(metadata);
        }

        public Task<IReadOnlyList<AgentMetadata>> GetAllAgentsAsync(CancellationToken cancellationToken = default)
        {
            var agents = _agents.Values.Where(a => a.IsActive).ToList();
            return Task.FromResult<IReadOnlyList<AgentMetadata>>(agents);
        }

        public Task<IReadOnlyList<AgentMetadata>> FindAgentsByCapabilityAsync(string capabilityName, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(capabilityName);

            var agents = _agents.Values
                .Where(a => a.IsActive && a.Capabilities.Any(c => c.Name.Equals(capabilityName, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return Task.FromResult<IReadOnlyList<AgentMetadata>>(agents);
        }

        public Task<IReadOnlyList<AgentMetadata>> FindAgentsByTypeAsync(AgentType type, CancellationToken cancellationToken = default)
        {
            var agents = _agents.Values
                .Where(a => a.IsActive && a.Type == type)
                .ToList();

            return Task.FromResult<IReadOnlyList<AgentMetadata>>(agents);
        }

        public Task<bool> IsAgentRegisteredAsync(string agentId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(agentId);

            var exists = _agents.ContainsKey(agentId);
            return Task.FromResult(exists);
        }

        public Task UpdateAgentAsync(AgentMetadata metadata, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(metadata);
            ArgumentException.ThrowIfNullOrWhiteSpace(metadata.Id, nameof(metadata.Id));

            if (!_agents.TryGetValue(metadata.Id, out var existingMetadata))
            {
                throw new InvalidOperationException($"Agent with ID '{metadata.Id}' is not registered");
            }

            metadata.CreatedAt = existingMetadata.CreatedAt;
            metadata.UpdatedAt = DateTime.UtcNow;

            _agents[metadata.Id] = metadata;
            return Task.CompletedTask;
        }
    }
}
