using AI_AI_Agent.Infrastructure.Services.Planning;
using AI_AI_Agent.Infrastructure.Services.State;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Recovery
{
    /// <summary>
    /// Error recovery service for classification, self-healing, and graceful degradation
    /// </summary>
    public class ErrorRecoveryService
    {
        private readonly ILogger<ErrorRecoveryService> _logger;
        private readonly TaskPlanningService _taskPlanning;
        private readonly StateManagementService _stateManagement;
        
        // Recovery history
        private readonly Dictionary<string, List<RecoveryAttempt>> _recoveryHistory = new();
        private readonly Dictionary<string, RecoveryStrategy> _learnedStrategies = new();

        public ErrorRecoveryService(
            ILogger<ErrorRecoveryService> logger,
            TaskPlanningService taskPlanning,
            StateManagementService stateManagement)
        {
            _logger = logger;
            _taskPlanning = taskPlanning;
            _stateManagement = stateManagement;
        }

        #region Error Classification

        /// <summary>
        /// Classify an error to determine recovery strategy
        /// </summary>
        public ErrorClassification ClassifyError(Exception exception, string context)
        {
            var classification = new ErrorClassification
            {
                Exception = exception,
                Context = context,
                Timestamp = DateTime.UtcNow
            };

            try
            {
                var exceptionType = exception.GetType().Name;
                var message = exception.Message.ToLower();

                // Classify by exception type
                if (exception is TimeoutException)
                {
                    classification.Category = ErrorCategory.Timeout;
                    classification.Severity = ErrorSeverity.Medium;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Retry with increased timeout";
                }
                else if (exception is UnauthorizedAccessException)
                {
                    classification.Category = ErrorCategory.Authorization;
                    classification.Severity = ErrorSeverity.High;
                    classification.Recoverable = false;
                    classification.SuggestedAction = "Request authorization or use alternative approach";
                }
                else if (exception is System.Net.Http.HttpRequestException)
                {
                    classification.Category = ErrorCategory.Network;
                    classification.Severity = ErrorSeverity.Medium;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Retry with exponential backoff";
                }
                else if (exception is ArgumentException || exception is ArgumentNullException)
                {
                    classification.Category = ErrorCategory.Validation;
                    classification.Severity = ErrorSeverity.Medium;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Validate and sanitize inputs";
                }
                else if (exception is InvalidOperationException)
                {
                    classification.Category = ErrorCategory.State;
                    classification.Severity = ErrorSeverity.Medium;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Reset state or use checkpoint";
                }
                else if (exception is OutOfMemoryException)
                {
                    classification.Category = ErrorCategory.Resource;
                    classification.Severity = ErrorSeverity.Critical;
                    classification.Recoverable = false;
                    classification.SuggestedAction = "Graceful degradation - reduce scope";
                }
                else if (message.Contains("rate limit") || message.Contains("quota"))
                {
                    classification.Category = ErrorCategory.RateLimit;
                    classification.Severity = ErrorSeverity.High;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Wait and retry with exponential backoff";
                }
                else if (message.Contains("not found") || message.Contains("404"))
                {
                    classification.Category = ErrorCategory.NotFound;
                    classification.Severity = ErrorSeverity.Low;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Use alternative resource or skip";
                }
                else
                {
                    classification.Category = ErrorCategory.Unknown;
                    classification.Severity = ErrorSeverity.Medium;
                    classification.Recoverable = true;
                    classification.SuggestedAction = "Retry with default strategy";
                }

                _logger.LogInformation("Classified error as {Category} with {Severity} severity",
                    classification.Category, classification.Severity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during error classification");
                classification.Category = ErrorCategory.Unknown;
                classification.Severity = ErrorSeverity.High;
            }

            return classification;
        }

        #endregion

        #region Recovery Strategies

        /// <summary>
        /// Suggest recovery strategy based on error classification
        /// </summary>
        public RecoveryStrategy SuggestRecovery(ErrorClassification classification)
        {
            var strategy = new RecoveryStrategy
            {
                Classification = classification
            };

            // Check if we have a learned strategy for this error type
            var errorKey = GetErrorKey(classification);
            if (_learnedStrategies.TryGetValue(errorKey, out var learnedStrategy))
            {
                _logger.LogInformation("Using learned strategy for {Category}", classification.Category);
                return learnedStrategy;
            }

            // Determine strategy based on classification
            switch (classification.Category)
            {
                case ErrorCategory.Timeout:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        MaxAttempts = 3,
                        DelayMs = 2000,
                        Parameters = new Dictionary<string, object>
                        {
                            ["increaseTimeout"] = true,
                            ["backoffMultiplier"] = 2
                        }
                    });
                    break;

                case ErrorCategory.Network:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        MaxAttempts = 5,
                        DelayMs = 1000,
                        Parameters = new Dictionary<string, object>
                        {
                            ["exponentialBackoff"] = true,
                            ["maxDelay"] = 30000
                        }
                    });
                    break;

                case ErrorCategory.RateLimit:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Wait,
                        DelayMs = 60000,
                        Parameters = new Dictionary<string, object>
                        {
                            ["checkRateLimit"] = true
                        }
                    });
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        MaxAttempts = 1
                    });
                    break;

                case ErrorCategory.Authorization:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Alternative,
                        Parameters = new Dictionary<string, object>
                        {
                            ["requestApproval"] = true
                        }
                    });
                    break;

                case ErrorCategory.Validation:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Sanitize,
                        Parameters = new Dictionary<string, object>
                        {
                            ["validateInputs"] = true
                        }
                    });
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        MaxAttempts = 1
                    });
                    break;

                case ErrorCategory.State:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Rollback,
                        Parameters = new Dictionary<string, object>
                        {
                            ["restoreCheckpoint"] = true
                        }
                    });
                    break;

                case ErrorCategory.Resource:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Degrade,
                        Parameters = new Dictionary<string, object>
                        {
                            ["reduceScope"] = true,
                            ["simplifyTask"] = true
                        }
                    });
                    break;

                case ErrorCategory.NotFound:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Alternative,
                        Parameters = new Dictionary<string, object>
                        {
                            ["findAlternative"] = true
                        }
                    });
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Skip,
                        Parameters = new Dictionary<string, object>
                        {
                            ["continueWithoutResource"] = true
                        }
                    });
                    break;

                default:
                    strategy.Actions.Add(new RecoveryAction
                    {
                        Type = RecoveryActionType.Retry,
                        MaxAttempts = 2,
                        DelayMs = 1000
                    });
                    break;
            }

            return strategy;
        }

        /// <summary>
        /// Apply recovery strategy
        /// </summary>
        public async Task<RecoveryResult> ApplyRecoveryAsync<T>(
            Func<Task<T>> operation,
            ErrorClassification classification,
            CancellationToken cancellationToken = default)
        {
            var result = new RecoveryResult
            {
                Classification = classification,
                StartTime = DateTime.UtcNow
            };

            try
            {
                var strategy = SuggestRecovery(classification);
                _logger.LogInformation("Applying recovery strategy with {Count} actions", 
                    strategy.Actions.Count);

                foreach (var action in strategy.Actions)
                {
                    result.ActionsAttempted++;

                    try
                    {
                        switch (action.Type)
                        {
                            case RecoveryActionType.Retry:
                                var success = await RetryWithBackoffAsync(
                                    operation,
                                    action.MaxAttempts,
                                    action.DelayMs,
                                    cancellationToken);
                                
                                if (success)
                                {
                                    result.Recovered = true;
                                    result.SuccessfulAction = action.Type.ToString();
                                    RecordSuccessfulRecovery(classification, action.Type);
                                    return result;
                                }
                                break;

                            case RecoveryActionType.Wait:
                                _logger.LogInformation("Waiting {Ms}ms before retry", action.DelayMs);
                                await Task.Delay(action.DelayMs, cancellationToken);
                                break;

                            case RecoveryActionType.Rollback:
                                _logger.LogInformation("Rolling back to previous state");
                                // In real system, would call state management service with transaction
                                // For now, just log the rollback
                                result.Recovered = true;
                                result.SuccessfulAction = "Rollback";
                                return result;

                            case RecoveryActionType.Degrade:
                                _logger.LogInformation("Applying graceful degradation");
                                result.Recovered = true;
                                result.SuccessfulAction = "Degrade";
                                result.Message = "Gracefully degraded - reduced scope";
                                return result;

                            case RecoveryActionType.Skip:
                                _logger.LogInformation("Skipping failed operation");
                                result.Recovered = true;
                                result.SuccessfulAction = "Skip";
                                result.Message = "Operation skipped - continuing without it";
                                return result;

                            case RecoveryActionType.Alternative:
                                _logger.LogInformation("Attempting alternative approach");
                                result.Message = "Alternative approach required - manual intervention";
                                break;

                            case RecoveryActionType.Sanitize:
                                _logger.LogInformation("Sanitizing inputs and retrying");
                                // In real system, would sanitize inputs
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Recovery action {Action} failed", action.Type);
                        result.Errors.Add($"{action.Type}: {ex.Message}");
                    }
                }

                // If we get here, recovery failed
                result.Recovered = false;
                result.Message = "All recovery actions exhausted";
            }
            catch (Exception ex)
            {
                result.Recovered = false;
                result.Message = $"Recovery process failed: {ex.Message}";
                _logger.LogError(ex, "Error during recovery");
            }
            finally
            {
                result.EndTime = DateTime.UtcNow;
                result.Duration = (result.EndTime - result.StartTime).TotalSeconds;
            }

            return result;
        }

        #endregion

        #region Self-Healing

        /// <summary>
        /// Retry operation with exponential backoff
        /// </summary>
        private async Task<bool> RetryWithBackoffAsync<T>(
            Func<Task<T>> operation,
            int maxAttempts,
            int initialDelayMs,
            CancellationToken cancellationToken)
        {
            var attempt = 0;
            var delay = initialDelayMs;

            while (attempt < maxAttempts)
            {
                attempt++;
                
                try
                {
                    _logger.LogInformation("Retry attempt {Attempt}/{Max}", attempt, maxAttempts);
                    await operation();
                    _logger.LogInformation("Retry successful on attempt {Attempt}", attempt);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Retry attempt {Attempt} failed", attempt);
                    
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(delay, cancellationToken);
                        delay *= 2; // Exponential backoff
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Record successful recovery for learning
        /// </summary>
        private void RecordSuccessfulRecovery(ErrorClassification classification, RecoveryActionType actionType)
        {
            var errorKey = GetErrorKey(classification);
            
            if (!_recoveryHistory.ContainsKey(errorKey))
            {
                _recoveryHistory[errorKey] = new List<RecoveryAttempt>();
            }

            _recoveryHistory[errorKey].Add(new RecoveryAttempt
            {
                Timestamp = DateTime.UtcNow,
                ActionType = actionType,
                Success = true
            });

            // Learn from successful recoveries
            LearnRecoveryStrategy(errorKey, actionType);
        }

        /// <summary>
        /// Learn recovery strategy from history
        /// </summary>
        private void LearnRecoveryStrategy(string errorKey, RecoveryActionType successfulAction)
        {
            if (!_recoveryHistory.TryGetValue(errorKey, out var history))
                return;

            // Find most successful action
            var successfulActions = history
                .Where(a => a.Success)
                .GroupBy(a => a.ActionType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault();

            if (successfulActions != null && successfulActions.Count() >= 3)
            {
                // Create learned strategy
                var strategy = new RecoveryStrategy
                {
                    Actions = new List<RecoveryAction>
                    {
                        new RecoveryAction
                        {
                            Type = successfulActions.Key,
                            MaxAttempts = 3,
                            DelayMs = 1000
                        }
                    }
                };

                _learnedStrategies[errorKey] = strategy;
                _logger.LogInformation("Learned recovery strategy for {Key}: {Action}",
                    errorKey, successfulActions.Key);
            }
        }

        #endregion

        #region Graceful Degradation

        /// <summary>
        /// Apply graceful degradation
        /// </summary>
        public async Task<DegradationResult> DegradeGracefullyAsync(
            string taskDescription,
            Dictionary<string, object> context,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Applying graceful degradation for: {Task}", taskDescription);

            var result = new DegradationResult
            {
                OriginalTask = taskDescription
            };

            try
            {
                // Replan with reduced scope
                var simplifiedGoal = $"Simplified: {taskDescription}";
                var plan = _taskPlanning.CreatePlan(simplifiedGoal, JsonSerializer.Serialize(context));

                // Create simplified tasks (in real system, would use AI)
                var essentialTask = new Domain.Planning.PlanTask
                {
                    Description = $"Essential: {taskDescription}",
                    Priority = 10, // High priority
                    RequiredAgentType = Domain.Agents.AgentType.Research
                };
                _taskPlanning.AddTask(plan, essentialTask);

                result.RemainingTasks = 1;
                result.RemovedTasks = 0;
                result.Success = true;
                result.Message = $"Simplified to essential task only";

                _logger.LogInformation("Degradation successful: {Message}", result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Degradation failed: {ex.Message}";
                _logger.LogError(ex, "Error during graceful degradation");
            }

            return result;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Generate error key for classification
        /// </summary>
        private string GetErrorKey(ErrorClassification classification)
        {
            return $"{classification.Category}:{classification.Severity}";
        }

        /// <summary>
        /// Get recovery statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            var totalAttempts = _recoveryHistory.Values.Sum(h => h.Count);
            var successfulAttempts = _recoveryHistory.Values.Sum(h => h.Count(a => a.Success));
            var successRate = totalAttempts > 0 ? (double)successfulAttempts / totalAttempts : 0;

            return new Dictionary<string, object>
            {
                ["TotalRecoveryAttempts"] = totalAttempts,
                ["SuccessfulRecoveries"] = successfulAttempts,
                ["SuccessRate"] = successRate,
                ["LearnedStrategies"] = _learnedStrategies.Count,
                ["UniqueErrorTypes"] = _recoveryHistory.Count
            };
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Error classification result
    /// </summary>
    public class ErrorClassification
    {
        public Exception? Exception { get; set; }
        public string Context { get; set; } = string.Empty;
        public ErrorCategory Category { get; set; }
        public ErrorSeverity Severity { get; set; }
        public bool Recoverable { get; set; }
        public string SuggestedAction { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Error categories
    /// </summary>
    public enum ErrorCategory
    {
        Unknown,
        Timeout,
        Network,
        Authorization,
        Validation,
        State,
        Resource,
        RateLimit,
        NotFound
    }

    /// <summary>
    /// Error severity levels
    /// </summary>
    public enum ErrorSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Recovery strategy
    /// </summary>
    public class RecoveryStrategy
    {
        public ErrorClassification? Classification { get; set; }
        public List<RecoveryAction> Actions { get; set; } = new();
    }

    /// <summary>
    /// Recovery action
    /// </summary>
    public class RecoveryAction
    {
        public RecoveryActionType Type { get; set; }
        public int MaxAttempts { get; set; } = 1;
        public int DelayMs { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Recovery action types
    /// </summary>
    public enum RecoveryActionType
    {
        Retry,
        Wait,
        Rollback,
        Alternative,
        Degrade,
        Skip,
        Sanitize
    }

    /// <summary>
    /// Recovery result
    /// </summary>
    public class RecoveryResult
    {
        public ErrorClassification? Classification { get; set; }
        public bool Recovered { get; set; }
        public string SuccessfulAction { get; set; } = string.Empty;
        public int ActionsAttempted { get; set; }
        public List<string> Errors { get; set; } = new();
        public string Message { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public double Duration { get; set; }
    }

    /// <summary>
    /// Recovery attempt record
    /// </summary>
    public class RecoveryAttempt
    {
        public DateTime Timestamp { get; set; }
        public RecoveryActionType ActionType { get; set; }
        public bool Success { get; set; }
    }

    /// <summary>
    /// Degradation result
    /// </summary>
    public class DegradationResult
    {
        public string OriginalTask { get; set; } = string.Empty;
        public int RemainingTasks { get; set; }
        public int RemovedTasks { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    #endregion
}
