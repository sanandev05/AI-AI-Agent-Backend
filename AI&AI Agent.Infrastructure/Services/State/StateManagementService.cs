using AI_AI_Agent.Domain.State;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.State
{
    /// <summary>
    /// State management service with checkpoint/resume and transactions
    /// </summary>
    public class StateManagementService
    {
        private readonly ConcurrentDictionary<string, AgentState> _states = new();
        private readonly ConcurrentDictionary<string, List<AgentState>> _checkpoints = new();
        private readonly ConcurrentDictionary<string, StateTransaction> _activeTransactions = new();

        /// <summary>
        /// Create a new agent state
        /// </summary>
        public AgentState CreateState(string agentId, string userId, string sessionId)
        {
            var state = new AgentState
            {
                AgentId = agentId,
                UserId = userId,
                SessionId = sessionId
            };

            _states[state.Id] = state;
            return state;
        }

        /// <summary>
        /// Get agent state by ID
        /// </summary>
        public AgentState? GetState(string stateId)
        {
            _states.TryGetValue(stateId, out var state);
            return state;
        }

        /// <summary>
        /// Get all states for an agent
        /// </summary>
        public List<AgentState> GetAgentStates(string agentId)
        {
            return _states.Values.Where(s => s.AgentId == agentId).ToList();
        }

        /// <summary>
        /// Get all states for a user
        /// </summary>
        public List<AgentState> GetUserStates(string userId)
        {
            return _states.Values.Where(s => s.UserId == userId).ToList();
        }

        /// <summary>
        /// Update agent state data
        /// </summary>
        public void UpdateState(AgentState state, string key, object value)
        {
            state.Data[key] = value;
            state.UpdatedAt = DateTime.UtcNow;
            state.Version++;
        }

        /// <summary>
        /// Update multiple state values atomically
        /// </summary>
        public void UpdateStateBatch(AgentState state, Dictionary<string, object> updates)
        {
            foreach (var kvp in updates)
            {
                state.Data[kvp.Key] = kvp.Value;
            }
            state.UpdatedAt = DateTime.UtcNow;
            state.Version++;
        }

        /// <summary>
        /// Create a checkpoint of the current state
        /// </summary>
        public string CreateCheckpoint(AgentState state, string? description = null)
        {
            var checkpointId = Guid.NewGuid().ToString();
            
            // Deep clone the state
            var checkpoint = CloneState(state);
            checkpoint.CheckpointId = checkpointId;
            checkpoint.CheckpointedAt = DateTime.UtcNow;
            checkpoint.Metadata["checkpoint_description"] = description ?? "Auto checkpoint";
            checkpoint.Metadata["original_state_id"] = state.Id;

            // Store checkpoint
            if (!_checkpoints.ContainsKey(state.Id))
            {
                _checkpoints[state.Id] = new List<AgentState>();
            }
            _checkpoints[state.Id].Add(checkpoint);

            state.CheckpointId = checkpointId;
            state.CheckpointedAt = DateTime.UtcNow;

            return checkpointId;
        }

        /// <summary>
        /// Resume from a checkpoint
        /// </summary>
        public AgentState? ResumeFromCheckpoint(string stateId, string checkpointId)
        {
            if (!_checkpoints.TryGetValue(stateId, out var checkpoints))
            {
                return null;
            }

            var checkpoint = checkpoints.FirstOrDefault(c => c.CheckpointId == checkpointId);
            if (checkpoint == null)
            {
                return null;
            }

            // Create a new state from the checkpoint
            var restoredState = CloneState(checkpoint);
            restoredState.Id = Guid.NewGuid().ToString(); // New state ID
            restoredState.Metadata["restored_from"] = checkpointId;
            restoredState.Metadata["restored_at"] = DateTime.UtcNow.ToString("o");
            restoredState.Status = AgentStateStatus.Active;

            _states[restoredState.Id] = restoredState;
            return restoredState;
        }

        /// <summary>
        /// Get all checkpoints for a state
        /// </summary>
        public List<AgentState> GetCheckpoints(string stateId)
        {
            _checkpoints.TryGetValue(stateId, out var checkpoints);
            return checkpoints ?? new List<AgentState>();
        }

        /// <summary>
        /// Begin a transaction for atomic state updates
        /// </summary>
        public StateTransaction BeginTransaction(AgentState state)
        {
            var transaction = new StateTransaction
            {
                StateId = state.Id,
                OriginalData = new Dictionary<string, object>(state.Data),
                OriginalVersion = state.Version
            };

            _activeTransactions[transaction.Id] = transaction;
            return transaction;
        }

        /// <summary>
        /// Commit a transaction
        /// </summary>
        public void CommitTransaction(StateTransaction transaction)
        {
            transaction.Status = TransactionStatus.Committed;
            transaction.CommittedAt = DateTime.UtcNow;
            _activeTransactions.TryRemove(transaction.Id, out _);
        }

        /// <summary>
        /// Rollback a transaction
        /// </summary>
        public void RollbackTransaction(StateTransaction transaction, AgentState state)
        {
            // Restore original data
            state.Data = new Dictionary<string, object>(transaction.OriginalData);
            state.Version = transaction.OriginalVersion;
            
            transaction.Status = TransactionStatus.RolledBack;
            transaction.RolledBackAt = DateTime.UtcNow;
            _activeTransactions.TryRemove(transaction.Id, out _);
        }

        /// <summary>
        /// Suspend agent state (pause execution)
        /// </summary>
        public void SuspendState(AgentState state, string reason = "")
        {
            state.Status = AgentStateStatus.Suspended;
            state.Metadata["suspended_at"] = DateTime.UtcNow.ToString("o");
            state.Metadata["suspension_reason"] = reason;
            state.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Resume suspended agent state
        /// </summary>
        public void ResumeState(AgentState state)
        {
            state.Status = AgentStateStatus.Active;
            state.Metadata["resumed_at"] = DateTime.UtcNow.ToString("o");
            state.UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Archive old states
        /// </summary>
        public int ArchiveOldStates(TimeSpan olderThan)
        {
            var cutoffTime = DateTime.UtcNow - olderThan;
            var toArchive = _states.Values
                .Where(s => s.UpdatedAt < cutoffTime && s.Status != AgentStateStatus.Active)
                .ToList();

            foreach (var state in toArchive)
            {
                state.Status = AgentStateStatus.Archived;
                state.Metadata["archived_at"] = DateTime.UtcNow.ToString("o");
            }

            return toArchive.Count;
        }

        /// <summary>
        /// Export state as JSON
        /// </summary>
        public string ExportState(AgentState state)
        {
            return JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        /// <summary>
        /// Import state from JSON
        /// </summary>
        public AgentState ImportState(string json)
        {
            var state = JsonSerializer.Deserialize<AgentState>(json);
            if (state == null)
            {
                throw new InvalidOperationException("Failed to deserialize state");
            }

            _states[state.Id] = state;
            return state;
        }

        /// <summary>
        /// Get state statistics
        /// </summary>
        public Dictionary<string, object> GetStateStatistics()
        {
            return new Dictionary<string, object>
            {
                { "TotalStates", _states.Count },
                { "ActiveStates", _states.Values.Count(s => s.Status == AgentStateStatus.Active) },
                { "SuspendedStates", _states.Values.Count(s => s.Status == AgentStateStatus.Suspended) },
                { "CompletedStates", _states.Values.Count(s => s.Status == AgentStateStatus.Completed) },
                { "FailedStates", _states.Values.Count(s => s.Status == AgentStateStatus.Failed) },
                { "ArchivedStates", _states.Values.Count(s => s.Status == AgentStateStatus.Archived) },
                { "TotalCheckpoints", _checkpoints.Values.Sum(c => c.Count) },
                { "ActiveTransactions", _activeTransactions.Count }
            };
        }

        /// <summary>
        /// Deep clone a state
        /// </summary>
        private AgentState CloneState(AgentState state)
        {
            var json = JsonSerializer.Serialize(state);
            return JsonSerializer.Deserialize<AgentState>(json)!;
        }
    }

    /// <summary>
    /// Represents a transaction for atomic state updates
    /// </summary>
    public class StateTransaction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string StateId { get; set; } = string.Empty;
        public Dictionary<string, object> OriginalData { get; set; } = new();
        public int OriginalVersion { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Active;
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CommittedAt { get; set; }
        public DateTime? RolledBackAt { get; set; }
    }

    public enum TransactionStatus
    {
        Active,
        Committed,
        RolledBack
    }
}
