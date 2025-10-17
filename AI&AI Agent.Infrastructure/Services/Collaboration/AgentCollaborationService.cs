using AI_AI_Agent.Domain.Collaboration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Collaboration
{
    /// <summary>
    /// Multi-agent collaboration service for agent communication and coordination
    /// </summary>
    public class AgentCollaborationService
    {
        private readonly ILogger<AgentCollaborationService> _logger;
        private readonly ConcurrentDictionary<string, List<AgentMessage>> _messageQueue = new();
        private readonly ConcurrentDictionary<string, List<TaskDelegation>> _delegations = new();
        private readonly ConcurrentDictionary<string, CollaborativeSession> _sessions = new();
        private readonly ConcurrentDictionary<string, ConflictCase> _conflicts = new();

        public AgentCollaborationService(ILogger<AgentCollaborationService> logger)
        {
            _logger = logger;
        }

        #region Agent Messaging

        public AgentMessage SendMessage(AgentMessage message)
        {
            message.SentAt = DateTime.UtcNow;
            
            if (!_messageQueue.ContainsKey(message.ToAgentId))
            {
                _messageQueue[message.ToAgentId] = new List<AgentMessage>();
            }
            
            _messageQueue[message.ToAgentId].Add(message);
            _logger.LogInformation("Agent {From} sent message to {To}", message.FromAgentId, message.ToAgentId);
            return message;
        }

        public List<AgentMessage> GetMessages(string agentId, bool unreadOnly = false)
        {
            if (!_messageQueue.TryGetValue(agentId, out var messages))
            {
                return new List<AgentMessage>();
            }

            return unreadOnly 
                ? messages.Where(m => !m.ReceivedAt.HasValue).ToList()
                : messages;
        }

        public AgentMessage? MarkMessageAsRead(string messageId)
        {
            foreach (var messages in _messageQueue.Values)
            {
                var message = messages.FirstOrDefault(m => m.Id == messageId);
                if (message != null)
                {
                    message.ReceivedAt = DateTime.UtcNow;
                    _logger.LogDebug("Marked message {MessageId} as read", messageId);
                    return message;
                }
            }
            return null;
        }

        #endregion

        #region Task Delegation

        public TaskDelegation DelegateTask(TaskDelegation delegation)
        {
            if (!_delegations.ContainsKey(delegation.ToAgentId))
            {
                _delegations[delegation.ToAgentId] = new List<TaskDelegation>();
            }

            _delegations[delegation.ToAgentId].Add(delegation);
            
            // Send message notification
            SendMessage(new AgentMessage
            {
                FromAgentId = delegation.FromAgentId,
                ToAgentId = delegation.ToAgentId,
                Type = AgentMessageType.TaskDelegation,
                Content = $"Task delegated: {delegation.TaskDescription}",
                Payload = new Dictionary<string, object> { ["delegationId"] = delegation.Id },
                RequiresResponse = true
            });

            _logger.LogInformation("Agent {From} delegated task to {To}: {Task}", 
                delegation.FromAgentId, delegation.ToAgentId, delegation.TaskDescription);

            return delegation;
        }

        public List<TaskDelegation> GetDelegatedTasks(string agentId)
        {
            return _delegations.TryGetValue(agentId, out var tasks) ? tasks : new List<TaskDelegation>();
        }

        public TaskDelegation? CompleteTaskDelegation(string delegationId, object result)
        {
            foreach (var tasks in _delegations.Values)
            {
                var task = tasks.FirstOrDefault(t => t.Id == delegationId);
                if (task != null)
                {
                    task.Status = "completed";
                    task.Result = result;
                    task.CompletedAt = DateTime.UtcNow;

                    // Send completion message
                    SendMessage(new AgentMessage
                    {
                        FromAgentId = task.ToAgentId,
                        ToAgentId = task.FromAgentId,
                        Type = AgentMessageType.Response,
                        Content = "Task completed",
                        Payload = new Dictionary<string, object> { ["delegationId"] = delegationId, ["result"] = result }
                    });

                    _logger.LogInformation("Task delegation {DelegationId} completed", delegationId);
                    return task;
                }
            }
            return null;
        }

        #endregion

        #region Collaborative Problem Solving

        public CollaborativeSession StartCollaborativeSession(string problem, List<string> participantAgentIds)
        {
            var session = new CollaborativeSession
            {
                Problem = problem,
                ParticipantAgentIds = participantAgentIds
            };

            _sessions[session.Id] = session;

            // Notify all participants
            foreach (var agentId in participantAgentIds)
            {
                SendMessage(new AgentMessage
                {
                    FromAgentId = "system",
                    ToAgentId = agentId,
                    Type = AgentMessageType.Notification,
                    Content = $"Invited to collaborative session: {problem}",
                    Payload = new Dictionary<string, object> { ["sessionId"] = session.Id }
                });
            }

            _logger.LogInformation("Started collaborative session {SessionId} with {Count} participants", 
                session.Id, participantAgentIds.Count);

            return session;
        }

        public void AddContribution(string sessionId, AgentContribution contribution)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Contributions.Add(contribution);
                _logger.LogInformation("Agent {AgentId} contributed to session {SessionId}", 
                    contribution.AgentId, sessionId);
            }
        }

        public CollaborativeSession? CompleteSession(string sessionId, object finalSolution)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Status = "completed";
                session.FinalSolution = finalSolution;
                session.CompletedAt = DateTime.UtcNow;
                _logger.LogInformation("Completed collaborative session {SessionId}", sessionId);
                return session;
            }
            return null;
        }

        #endregion

        #region Conflict Resolution

        public ConflictCase CreateConflict(List<string> conflictingAgentIds, string description)
        {
            var conflict = new ConflictCase
            {
                ConflictingAgentIds = conflictingAgentIds,
                ConflictDescription = description,
                Strategy = ConflictResolutionStrategy.HighestConfidence
            };

            _conflicts[conflict.Id] = conflict;
            _logger.LogWarning("Created conflict case {ConflictId}: {Description}", conflict.Id, description);

            return conflict;
        }

        public void AddConflictPosition(string conflictId, ConflictPosition position)
        {
            if (_conflicts.TryGetValue(conflictId, out var conflict))
            {
                conflict.Positions.Add(position);
                _logger.LogDebug("Added position from agent {AgentId} to conflict {ConflictId}", 
                    position.AgentId, conflictId);
            }
        }

        public ConflictCase? ResolveConflict(string conflictId, ConflictResolutionStrategy strategy)
        {
            if (!_conflicts.TryGetValue(conflictId, out var conflict))
            {
                return null;
            }

            conflict.Strategy = strategy;
            
            switch (strategy)
            {
                case ConflictResolutionStrategy.HighestConfidence:
                    var winner = conflict.Positions.OrderByDescending(p => p.Confidence).FirstOrDefault();
                    if (winner != null)
                    {
                        conflict.Resolution = $"Resolved using highest confidence: {winner.Position}";
                        conflict.ResolvedBy = winner.AgentId;
                    }
                    break;

                case ConflictResolutionStrategy.Voting:
                    // Simple majority
                    var grouped = conflict.Positions.GroupBy(p => p.Position);
                    var majority = grouped.OrderByDescending(g => g.Count()).FirstOrDefault();
                    if (majority != null)
                    {
                        conflict.Resolution = $"Resolved by voting: {majority.Key}";
                        conflict.ResolvedBy = "voting";
                    }
                    break;

                case ConflictResolutionStrategy.Consensus:
                    // All must agree
                    if (conflict.Positions.Select(p => p.Position).Distinct().Count() == 1)
                    {
                        conflict.Resolution = $"Consensus reached: {conflict.Positions.First().Position}";
                        conflict.ResolvedBy = "consensus";
                    }
                    else
                    {
                        conflict.Resolution = "No consensus reached";
                        conflict.ResolvedBy = "none";
                    }
                    break;

                default:
                    conflict.Resolution = "Requires human arbitration";
                    conflict.ResolvedBy = "pending";
                    break;
            }

            conflict.ResolvedAt = DateTime.UtcNow;
            _logger.LogInformation("Resolved conflict {ConflictId} using {Strategy}", conflictId, strategy);

            return conflict;
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalMessages"] = _messageQueue.Values.Sum(m => m.Count),
                ["unreadMessages"] = _messageQueue.Values.Sum(m => m.Count(msg => !msg.ReceivedAt.HasValue)),
                ["activeDelegations"] = _delegations.Values.Sum(d => d.Count(t => t.Status == "pending")),
                ["activeSessions"] = _sessions.Values.Count(s => s.Status == "active"),
                ["resolvedConflicts"] = _conflicts.Values.Count(c => c.ResolvedAt.HasValue),
                ["pendingConflicts"] = _conflicts.Values.Count(c => !c.ResolvedAt.HasValue)
            };
        }

        #endregion
    }
}
