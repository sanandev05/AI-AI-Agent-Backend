using AI_AI_Agent.Domain.Tools;
using AI_AI_Agent.Infrastructure.Services.Planning;
using AI_AI_Agent.Infrastructure.Services.Reasoning;
using AI_AI_Agent.Infrastructure.Services.DecisionMaking;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Autonomous
{
    /// <summary>
    /// Autonomous behavior service for self-directed task execution and learning
    /// </summary>
    public class AutonomousBehaviorService
    {
        private readonly ILogger<AutonomousBehaviorService> _logger;
        private readonly TaskPlanningService _taskPlanning;
        private readonly ReasoningEngine _reasoning;
        private readonly DecisionMakingService _decisionMaking;
        
        // Learning storage
        private readonly Dictionary<string, PerformanceMetrics> _taskPerformance = new();
        private readonly Dictionary<string, List<string>> _successfulStrategies = new();
        private readonly List<TaskSuggestion> _proactiveSuggestions = new();

        public AutonomousBehaviorService(
            ILogger<AutonomousBehaviorService> logger,
            TaskPlanningService taskPlanning,
            ReasoningEngine reasoning,
            DecisionMakingService decisionMaking)
        {
            _logger = logger;
            _taskPlanning = taskPlanning;
            _reasoning = reasoning;
            _decisionMaking = decisionMaking;
        }

        #region Goal-Oriented Execution

        /// <summary>
        /// Execute a goal autonomously by breaking it down and executing steps
        /// </summary>
        public async Task<AutonomousExecutionResult> ExecuteGoalAsync(
            string goal,
            Dictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting autonomous execution of goal: {Goal}", goal);

            var result = new AutonomousExecutionResult
            {
                Goal = goal,
                StartTime = DateTime.UtcNow
            };

            try
            {
                // Step 1: Create execution plan
                var plan = _taskPlanning.CreatePlan(goal, JsonSerializer.Serialize(context));
                
                // Create sample tasks (in real system, would use AI to decompose)
                var task1 = new Domain.Planning.PlanTask
                {
                    Description = $"Analyze: {goal}",
                    Priority = 10,
                    RequiredAgentType = Domain.Agents.AgentType.Research
                };
                var task2 = new Domain.Planning.PlanTask
                {
                    Description = $"Execute: {goal}",
                    Priority = 5,
                    RequiredAgentType = Domain.Agents.AgentType.Code
                };
                
                _taskPlanning.AddTask(plan, task1);
                _taskPlanning.AddTask(plan, task2);
                
                result.TasksCreated = plan.Tasks.Count;
                
                _logger.LogInformation("Goal decomposed into {Count} tasks", plan.Tasks.Count);

                // Step 2: Reason about approach
                var reasoningTrace = new Domain.Reasoning.ReasoningTrace();
                result.ReasoningSteps.Add($"Created plan with {plan.Tasks.Count} tasks");
                result.ReasoningSteps.Add($"Task 1: {task1.Description}");
                result.ReasoningSteps.Add($"Task 2: {task2.Description}");

                // Step 3: Execute tasks
                var executedTasks = new List<string>();
                var failedTasks = new List<string>();

                foreach (var task in plan.Tasks)
                {
                    try
                    {
                        _logger.LogInformation("Executing autonomous task: {Task}", task.Description);

                        // Simulate task execution (in real system, would use decision making and tools)
                        await Task.Delay(100, cancellationToken);
                        task.Status = Domain.Planning.PlanTaskStatus.Completed;
                        _taskPlanning.CompleteTask(plan, task, "Completed successfully");
                        executedTasks.Add(task.Id);
                        result.TasksCompleted++;
                        
                        // Record success for learning
                        RecordTaskSuccess(task.Description, "sequential");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing task {Task}", task.Description);
                        task.Status = Domain.Planning.PlanTaskStatus.Failed;
                        task.Error = ex.Message;
                        failedTasks.Add(task.Id);
                        result.TasksFailed++;
                        
                        // Record failure for learning
                        RecordTaskFailure(task.Description, ex.Message);
                    }
                }

                // Determine final status
                if (result.TasksFailed > 0)
                {
                    result.Status = "partial";
                }
                else if (result.TasksCompleted == result.TasksCreated)
                {
                    result.Status = "completed";
                }
                else
                {
                    result.Status = "incomplete";
                }

                result.EndTime = DateTime.UtcNow;
                result.Duration = (result.EndTime.Value - result.StartTime).TotalSeconds;

                _logger.LogInformation("Autonomous execution completed: {Status}, {Completed}/{Total} tasks",
                    result.Status, result.TasksCompleted, result.TasksCreated);
            }
            catch (Exception ex)
            {
                result.Status = "failed";
                result.Error = ex.Message;
                _logger.LogError(ex, "Autonomous execution failed for goal: {Goal}", goal);
            }

            return result;
        }

        #endregion

        #region Self-Directed Learning

        /// <summary>
        /// Record successful task execution for learning
        /// </summary>
        private void RecordTaskSuccess(string taskName, string strategy)
        {
            if (!_taskPerformance.ContainsKey(taskName))
            {
                _taskPerformance[taskName] = new PerformanceMetrics { TaskName = taskName };
            }

            var metrics = _taskPerformance[taskName];
            metrics.SuccessCount++;
            metrics.LastExecuted = DateTime.UtcNow;

            // Track successful strategies
            if (!_successfulStrategies.ContainsKey(taskName))
            {
                _successfulStrategies[taskName] = new List<string>();
            }
            _successfulStrategies[taskName].Add(strategy);

            _logger.LogDebug("Recorded success for task {Task}, strategy: {Strategy}", taskName, strategy);
        }

        /// <summary>
        /// Record failed task execution for learning
        /// </summary>
        private void RecordTaskFailure(string taskName, string error)
        {
            if (!_taskPerformance.ContainsKey(taskName))
            {
                _taskPerformance[taskName] = new PerformanceMetrics { TaskName = taskName };
            }

            var metrics = _taskPerformance[taskName];
            metrics.FailureCount++;
            metrics.LastError = error;
            metrics.LastExecuted = DateTime.UtcNow;

            _logger.LogDebug("Recorded failure for task {Task}, error: {Error}", taskName, error);
        }

        /// <summary>
        /// Get performance metrics for a task
        /// </summary>
        public PerformanceMetrics? GetTaskMetrics(string taskName)
        {
            return _taskPerformance.TryGetValue(taskName, out var metrics) ? metrics : null;
        }

        /// <summary>
        /// Get best strategy for a task based on history
        /// </summary>
        public string? GetBestStrategy(string taskName)
        {
            if (!_successfulStrategies.TryGetValue(taskName, out var strategies) || !strategies.Any())
            {
                return null;
            }

            // Return most frequently successful strategy
            return strategies
                .GroupBy(s => s)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }

        /// <summary>
        /// Learn from user feedback
        /// </summary>
        public void LearnFromFeedback(string taskName, string feedback, int rating)
        {
            if (!_taskPerformance.ContainsKey(taskName))
            {
                _taskPerformance[taskName] = new PerformanceMetrics { TaskName = taskName };
            }

            var metrics = _taskPerformance[taskName];
            metrics.FeedbackCount++;
            metrics.AverageRating = ((metrics.AverageRating * (metrics.FeedbackCount - 1)) + rating) / metrics.FeedbackCount;
            metrics.LastFeedback = feedback;

            _logger.LogInformation("Learned from feedback for task {Task}, rating: {Rating}", taskName, rating);
        }

        #endregion

        #region Proactive Suggestions

        /// <summary>
        /// Generate proactive task suggestions based on context
        /// </summary>
        public List<TaskSuggestion> GenerateProactiveSuggestions(Dictionary<string, object> context)
        {
            var suggestions = new List<TaskSuggestion>();

            try
            {
                // Analyze context for patterns
                if (context.ContainsKey("recentErrors") && context["recentErrors"] is int errorCount && errorCount > 5)
                {
                    suggestions.Add(new TaskSuggestion
                    {
                        Task = "Analyze error patterns and suggest fixes",
                        Reason = $"Detected {errorCount} recent errors",
                        Priority = "high",
                        Confidence = 0.85
                    });
                }

                if (context.ContainsKey("lastBackup") && context["lastBackup"] is DateTime lastBackup)
                {
                    var daysSinceBackup = (DateTime.UtcNow - lastBackup).TotalDays;
                    if (daysSinceBackup > 7)
                    {
                        suggestions.Add(new TaskSuggestion
                        {
                            Task = "Create system backup",
                            Reason = $"Last backup was {daysSinceBackup:F0} days ago",
                            Priority = "medium",
                            Confidence = 0.75
                        });
                    }
                }

                // Suggest based on successful patterns
                var topPerformingTasks = _taskPerformance.Values
                    .Where(m => m.SuccessRate > 0.8 && m.SuccessCount > 5)
                    .OrderByDescending(m => m.SuccessRate)
                    .Take(3)
                    .ToList();

                foreach (var task in topPerformingTasks)
                {
                    if ((DateTime.UtcNow - task.LastExecuted).TotalHours > 24)
                    {
                        suggestions.Add(new TaskSuggestion
                        {
                            Task = $"Re-run: {task.TaskName}",
                            Reason = $"High success rate ({task.SuccessRate:P0}) and not run recently",
                            Priority = "low",
                            Confidence = 0.65
                        });
                    }
                }

                _logger.LogInformation("Generated {Count} proactive suggestions", suggestions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating proactive suggestions");
            }

            return suggestions;
        }

        /// <summary>
        /// Schedule a task for autonomous execution
        /// </summary>
        public void ScheduleTask(TaskSuggestion suggestion, DateTime scheduledTime)
        {
            suggestion.ScheduledTime = scheduledTime;
            _proactiveSuggestions.Add(suggestion);
            
            _logger.LogInformation("Scheduled task: {Task} for {Time}", 
                suggestion.Task, scheduledTime);
        }

        /// <summary>
        /// Get pending scheduled tasks
        /// </summary>
        public List<TaskSuggestion> GetScheduledTasks()
        {
            return _proactiveSuggestions
                .Where(s => s.ScheduledTime.HasValue && s.ScheduledTime > DateTime.UtcNow)
                .OrderBy(s => s.ScheduledTime)
                .ToList();
        }

        #endregion

        #region Adaptive Strategies

        /// <summary>
        /// Select strategy adaptively based on context and past performance
        /// </summary>
        public string SelectAdaptiveStrategy(string taskName, Dictionary<string, object> context)
        {
            // Check if we have historical data
            var bestStrategy = GetBestStrategy(taskName);
            if (bestStrategy != null)
            {
                _logger.LogInformation("Using learned strategy for {Task}: {Strategy}", 
                    taskName, bestStrategy);
                return bestStrategy;
            }

            // Use simple heuristic for new tasks
            var strategies = new[] { "sequential", "parallel", "adaptive", "conservative" };
            return strategies[0]; // Default to sequential
        }

        /// <summary>
        /// Adapt behavior based on recent performance
        /// </summary>
        public AdaptationRecommendation AnalyzeAndAdapt()
        {
            var recommendation = new AdaptationRecommendation();

            try
            {
                var totalTasks = _taskPerformance.Values.Sum(m => m.SuccessCount + m.FailureCount);
                if (totalTasks == 0)
                {
                    recommendation.Message = "Insufficient data for adaptation";
                    return recommendation;
                }

                var overallSuccessRate = _taskPerformance.Values
                    .Average(m => m.SuccessRate);

                recommendation.CurrentSuccessRate = overallSuccessRate;

                if (overallSuccessRate < 0.6)
                {
                    recommendation.Recommendation = "Increase validation and use conservative strategies";
                    recommendation.Severity = "high";
                }
                else if (overallSuccessRate < 0.8)
                {
                    recommendation.Recommendation = "Balance between speed and safety";
                    recommendation.Severity = "medium";
                }
                else
                {
                    recommendation.Recommendation = "Current strategies working well, can increase automation";
                    recommendation.Severity = "low";
                }

                // Identify problem areas
                var problematicTasks = _taskPerformance.Values
                    .Where(m => m.SuccessRate < 0.5 && (m.SuccessCount + m.FailureCount) > 3)
                    .OrderBy(m => m.SuccessRate)
                    .Take(5)
                    .ToList();

                recommendation.ProblematicTasks.AddRange(
                    problematicTasks.Select(t => $"{t.TaskName} ({t.SuccessRate:P0})"));

                _logger.LogInformation("Adaptation analysis: {Rate:P0} success rate, {Recommendation}",
                    overallSuccessRate, recommendation.Recommendation);
            }
            catch (Exception ex)
            {
                recommendation.Message = $"Error during adaptation analysis: {ex.Message}";
                _logger.LogError(ex, "Error analyzing and adapting");
            }

            return recommendation;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get autonomous behavior statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var totalTasks = _taskPerformance.Values.Sum(m => m.SuccessCount + m.FailureCount);
            var totalSuccess = _taskPerformance.Values.Sum(m => m.SuccessCount);
            var overallSuccessRate = totalTasks > 0 ? (double)totalSuccess / totalTasks : 0;

            return new Dictionary<string, object>
            {
                ["TotalTasksExecuted"] = totalTasks,
                ["SuccessfulTasks"] = totalSuccess,
                ["FailedTasks"] = _taskPerformance.Values.Sum(m => m.FailureCount),
                ["OverallSuccessRate"] = overallSuccessRate,
                ["UniqueTaskTypes"] = _taskPerformance.Count,
                ["LearnedStrategies"] = _successfulStrategies.Count,
                ["ScheduledTasks"] = _proactiveSuggestions.Count,
                ["AverageFeedbackRating"] = _taskPerformance.Values
                    .Where(m => m.FeedbackCount > 0)
                    .Average(m => m.AverageRating)
            };
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Result of autonomous goal execution
    /// </summary>
    public class AutonomousExecutionResult
    {
        public string Goal { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public int TasksCreated { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksFailed { get; set; }
        public int TasksSkipped { get; set; }
        public int TasksDeferred { get; set; }
        public List<string> ReasoningSteps { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double Duration { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Performance metrics for a task
    /// </summary>
    public class PerformanceMetrics
    {
        public string TaskName { get; set; } = string.Empty;
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public double SuccessRate => (SuccessCount + FailureCount) > 0 
            ? (double)SuccessCount / (SuccessCount + FailureCount) 
            : 0;
        public DateTime LastExecuted { get; set; }
        public string? LastError { get; set; }
        public int FeedbackCount { get; set; }
        public double AverageRating { get; set; }
        public string? LastFeedback { get; set; }
    }

    /// <summary>
    /// Proactive task suggestion
    /// </summary>
    public class TaskSuggestion
    {
        public string Task { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Priority { get; set; } = "medium";
        public double Confidence { get; set; }
        public DateTime? ScheduledTime { get; set; }
    }

    /// <summary>
    /// Adaptation recommendation
    /// </summary>
    public class AdaptationRecommendation
    {
        public double CurrentSuccessRate { get; set; }
        public string Recommendation { get; set; } = string.Empty;
        public string Severity { get; set; } = "low";
        public List<string> ProblematicTasks { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}
