using AI_AI_Agent.Domain.Collaboration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Collaboration
{
    /// <summary>
    /// Team agent service for creating and managing agent teams
    /// </summary>
    public class TeamAgentService
    {
        private readonly ILogger<TeamAgentService> _logger;
        private readonly ConcurrentDictionary<string, AgentTeam> _teams = new();
        private readonly ConcurrentDictionary<string, ParallelExecutionPlan> _executionPlans = new();

        public TeamAgentService(ILogger<TeamAgentService> logger)
        {
            _logger = logger;
        }

        #region Team Management

        public AgentTeam CreateTeam(string name, string leaderAgentId, string goal)
        {
            var team = new AgentTeam
            {
                Name = name,
                LeaderAgentId = leaderAgentId,
                Goal = goal,
                Status = TeamStatus.Forming
            };

            _teams[team.Id] = team;
            _logger.LogInformation("Created team {TeamName} with leader {LeaderId}", name, leaderAgentId);

            return team;
        }

        public AgentTeam? GetTeam(string teamId)
        {
            return _teams.TryGetValue(teamId, out var team) ? team : null;
        }

        public List<AgentTeam> GetAllTeams()
        {
            return _teams.Values.ToList();
        }

        public void AddTeamMember(string teamId, TeamMember member)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                team.Members.Add(member);
                _logger.LogInformation("Added member {AgentId} with role {Role} to team {TeamId}", 
                    member.AgentId, member.Role, teamId);
            }
        }

        public void RemoveTeamMember(string teamId, string agentId)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                var member = team.Members.FirstOrDefault(m => m.AgentId == agentId);
                if (member != null)
                {
                    team.Members.Remove(member);
                    _logger.LogInformation("Removed member {AgentId} from team {TeamId}", agentId, teamId);
                }
            }
        }

        public void ActivateTeam(string teamId)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                team.Status = TeamStatus.Active;
                _logger.LogInformation("Activated team {TeamId}", teamId);
            }
        }

        public void DisbandTeam(string teamId)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                team.Status = TeamStatus.Disbanded;
                _logger.LogInformation("Disbanded team {TeamId}", teamId);
            }
        }

        #endregion

        #region Role-Based Collaboration

        public List<TeamMember> GetMembersByRole(string teamId, string role)
        {
            var team = GetTeam(teamId);
            if (team == null) return new List<TeamMember>();

            return team.Members.Where(m => m.Role.Equals(role, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void AssignResponsibility(string teamId, string agentId, string responsibility)
        {
            var team = GetTeam(teamId);
            if (team == null) return;

            var member = team.Members.FirstOrDefault(m => m.AgentId == agentId);
            if (member != null && !member.Responsibilities.Contains(responsibility))
            {
                member.Responsibilities.Add(responsibility);
                _logger.LogInformation("Assigned responsibility '{Responsibility}' to {AgentId} in team {TeamId}", 
                    responsibility, agentId, teamId);
            }
        }

        #endregion

        #region Parallel Execution

        public ParallelExecutionPlan CreateExecutionPlan(string teamId, List<ParallelTask> tasks)
        {
            var plan = new ParallelExecutionPlan
            {
                TeamId = teamId,
                Tasks = tasks,
                Status = "pending",
                StartedAt = DateTime.UtcNow
            };

            _executionPlans[plan.Id] = plan;
            _logger.LogInformation("Created execution plan {PlanId} for team {TeamId} with {TaskCount} tasks", 
                plan.Id, teamId, tasks.Count);

            return plan;
        }

        public async Task<ParallelExecutionPlan> ExecuteInParallelAsync(
            string planId,
            CancellationToken cancellationToken = default)
        {
            if (!_executionPlans.TryGetValue(planId, out var plan))
            {
                throw new InvalidOperationException($"Execution plan {planId} not found");
            }

            plan.Status = "running";
            _logger.LogInformation("Starting parallel execution of plan {PlanId}", planId);

            // Execute all tasks in parallel
            var tasks = plan.Tasks.Select(task => ExecuteTaskAsync(task, cancellationToken));
            await Task.WhenAll(tasks);

            // Check if all tasks completed successfully
            plan.Status = plan.Tasks.All(t => t.Status == "completed") ? "completed" : "failed";
            plan.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Completed parallel execution of plan {PlanId} with status {Status}", 
                planId, plan.Status);

            return plan;
        }

        private async Task ExecuteTaskAsync(ParallelTask task, CancellationToken cancellationToken)
        {
            try
            {
                task.Status = "running";
                _logger.LogDebug("Executing task {TaskId} for agent {AgentId}", task.Id, task.AssignedAgentId);

                // Simulate task execution
                await Task.Delay(100, cancellationToken);

                task.Status = "completed";
                task.Result = $"Task {task.Description} completed successfully";
                task.CompletedAt = DateTime.UtcNow;

                _logger.LogDebug("Completed task {TaskId}", task.Id);
            }
            catch (Exception ex)
            {
                task.Status = "failed";
                _logger.LogError(ex, "Failed to execute task {TaskId}", task.Id);
            }
        }

        public ParallelExecutionPlan? GetExecutionPlan(string planId)
        {
            return _executionPlans.TryGetValue(planId, out var plan) ? plan : null;
        }

        #endregion

        #region Result Synthesis

        public object SynthesizeResults(string teamId, List<object> results)
        {
            var team = GetTeam(teamId);
            if (team == null)
            {
                return new { error = "Team not found" };
            }

            _logger.LogInformation("Synthesizing {Count} results for team {TeamId}", results.Count, teamId);

            // Simple synthesis - combine all results
            var synthesis = new
            {
                teamId = teamId,
                teamName = team.Name,
                goal = team.Goal,
                contributorCount = results.Count,
                results = results,
                synthesizedAt = DateTime.UtcNow
            };

            return synthesis;
        }

        public object SynthesizeExecutionResults(string planId)
        {
            var plan = GetExecutionPlan(planId);
            if (plan == null)
            {
                return new { error = "Execution plan not found" };
            }

            var synthesis = new
            {
                planId = planId,
                teamId = plan.TeamId,
                totalTasks = plan.Tasks.Count,
                completedTasks = plan.Tasks.Count(t => t.Status == "completed"),
                failedTasks = plan.Tasks.Count(t => t.Status == "failed"),
                results = plan.Tasks.Select(t => new
                {
                    taskId = t.Id,
                    agentId = t.AssignedAgentId,
                    description = t.Description,
                    status = t.Status,
                    result = t.Result
                }).ToList(),
                synthesizedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Synthesized results for execution plan {PlanId}", planId);
            return synthesis;
        }

        #endregion

        #region Task Completion Tracking

        public void MarkTaskCompleted(string teamId, string taskDescription)
        {
            if (_teams.TryGetValue(teamId, out var team))
            {
                team.CompletedTasks.Add(taskDescription);
                _logger.LogInformation("Marked task completed for team {TeamId}: {Task}", teamId, taskDescription);
            }
        }

        public double GetTeamProgress(string teamId)
        {
            var team = GetTeam(teamId);
            if (team == null) return 0;

            var totalTasks = team.Members.Sum(m => m.Responsibilities.Count);
            if (totalTasks == 0) return 0;

            return (double)team.CompletedTasks.Count / totalTasks;
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalTeams"] = _teams.Count,
                ["activeTeams"] = _teams.Values.Count(t => t.Status == TeamStatus.Active),
                ["totalMembers"] = _teams.Values.Sum(t => t.Members.Count),
                ["totalExecutionPlans"] = _executionPlans.Count,
                ["completedPlans"] = _executionPlans.Values.Count(p => p.Status == "completed"),
                ["avgTeamSize"] = _teams.Values.Any() ? _teams.Values.Average(t => t.Members.Count) : 0
            };
        }

        public Dictionary<string, object> GetTeamStatistics(string teamId)
        {
            var team = GetTeam(teamId);
            if (team == null)
            {
                return new Dictionary<string, object> { ["error"] = "Team not found" };
            }

            return new Dictionary<string, object>
            {
                ["teamId"] = teamId,
                ["teamName"] = team.Name,
                ["status"] = team.Status.ToString(),
                ["memberCount"] = team.Members.Count,
                ["completedTasks"] = team.CompletedTasks.Count,
                ["progress"] = GetTeamProgress(teamId),
                ["createdAt"] = team.CreatedAt
            };
        }

        #endregion
    }
}
