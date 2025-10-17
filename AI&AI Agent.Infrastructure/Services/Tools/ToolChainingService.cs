using AI_AI_Agent.Domain.Tools;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Tools
{
    /// <summary>
    /// Tool chaining service for sequential and parallel tool execution
    /// </summary>
    public class ToolChainingService
    {
        private readonly ILogger<ToolChainingService> _logger;
        private readonly IEnumerable<ITool> _tools;
        private readonly ConcurrentDictionary<string, ToolChain> _activeChains = new();

        public ToolChainingService(
            ILogger<ToolChainingService> logger,
            IEnumerable<ITool> tools)
        {
            _logger = logger;
            _tools = tools;
        }

        #region Chain Creation

        /// <summary>
        /// Create a new tool chain
        /// </summary>
        public ToolChain CreateChain(string name)
        {
            var chain = new ToolChain
            {
                Name = name,
                Status = ToolChainStatus.Created
            };

            _activeChains[chain.Id] = chain;
            _logger.LogInformation("Created tool chain {ChainId}: {Name}", chain.Id, name);

            return chain;
        }

        /// <summary>
        /// Add a step to the chain
        /// </summary>
        public ToolChainStep AddStep(
            ToolChain chain,
            string toolName,
            Dictionary<string, object> arguments,
            string? outputVariable = null,
            List<string>? dependsOn = null)
        {
            var step = new ToolChainStep
            {
                ToolName = toolName,
                Arguments = arguments,
                OutputVariable = outputVariable,
                DependsOn = dependsOn ?? new List<string>()
            };

            chain.Steps.Add(step);
            chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Added step: {toolName}");

            _logger.LogDebug("Added step {StepId} to chain {ChainId}: {ToolName}", 
                step.Id, chain.Id, toolName);

            return step;
        }

        #endregion

        #region Chain Execution

        /// <summary>
        /// Execute a tool chain
        /// </summary>
        public async Task<ToolChain> ExecuteChainAsync(ToolChain chain, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Executing tool chain {ChainId}: {Name}", chain.Id, chain.Name);

            chain.Status = ToolChainStatus.Running;
            chain.StartedAt = DateTime.UtcNow;
            chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Chain execution started");

            try
            {
                while (HasPendingSteps(chain))
                {
                    var readySteps = GetReadySteps(chain);
                    
                    if (!readySteps.Any())
                    {
                        // Deadlock or all remaining steps depend on failed steps
                        var pendingSteps = chain.Steps.Where(s => s.Status == ToolChainStepStatus.Pending).ToList();
                        foreach (var step in pendingSteps)
                        {
                            step.Status = ToolChainStepStatus.Skipped;
                            step.Error = "Dependency not met";
                        }
                        break;
                    }

                    // Execute ready steps in parallel
                    var executionTasks = readySteps.Select(step => 
                        ExecuteStepAsync(chain, step, cancellationToken));
                    
                    await Task.WhenAll(executionTasks);
                }

                // Determine final status
                if (chain.Steps.Any(s => s.Status == ToolChainStepStatus.Failed))
                {
                    chain.Status = ToolChainStatus.Failed;
                    chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Chain failed with errors");
                }
                else
                {
                    chain.Status = ToolChainStatus.Completed;
                    chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Chain completed successfully");
                }
            }
            catch (Exception ex)
            {
                chain.Status = ToolChainStatus.Failed;
                chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Chain failed: {ex.Message}");
                _logger.LogError(ex, "Error executing chain {ChainId}", chain.Id);
            }
            finally
            {
                chain.CompletedAt = DateTime.UtcNow;
            }

            return chain;
        }

        /// <summary>
        /// Execute a single step
        /// </summary>
        private async Task ExecuteStepAsync(
            ToolChain chain,
            ToolChainStep step,
            CancellationToken cancellationToken)
        {
            step.Status = ToolChainStepStatus.Running;
            step.StartTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Executing step {StepId}: {ToolName}", step.Id, step.ToolName);

                // Find the tool
                var tool = _tools.FirstOrDefault(t => t.Name == step.ToolName);
                if (tool == null)
                {
                    throw new InvalidOperationException($"Tool {step.ToolName} not found");
                }

                // Resolve argument variables
                var resolvedArgs = ResolveArguments(step.Arguments, chain.Variables);

                // Convert to JsonElement for tool invocation
                var jsonString = JsonSerializer.Serialize(resolvedArgs);
                var jsonDoc = JsonDocument.Parse(jsonString);
                var jsonArgs = jsonDoc.RootElement;

                // Execute tool
                var result = await tool.InvokeAsync(jsonArgs, cancellationToken);

                step.Result = result;
                step.Status = ToolChainStepStatus.Completed;

                // Store result in variables if output variable is specified
                if (!string.IsNullOrEmpty(step.OutputVariable))
                {
                    chain.Variables[step.OutputVariable] = result;
                    _logger.LogDebug("Stored result in variable {Variable}", step.OutputVariable);
                }

                chain.ExecutionLog.Add(
                    $"[{DateTime.UtcNow:HH:mm:ss}] Step {step.ToolName} completed successfully");
            }
            catch (Exception ex)
            {
                step.Status = ToolChainStepStatus.Failed;
                step.Error = ex.Message;

                chain.ExecutionLog.Add(
                    $"[{DateTime.UtcNow:HH:mm:ss}] Step {step.ToolName} failed: {ex.Message}");

                _logger.LogError(ex, "Error executing step {StepId}: {ToolName}", 
                    step.Id, step.ToolName);
            }
            finally
            {
                step.EndTime = DateTime.UtcNow;
            }
        }

        private bool HasPendingSteps(ToolChain chain)
        {
            return chain.Steps.Any(s => s.Status == ToolChainStepStatus.Pending || 
                                       s.Status == ToolChainStepStatus.Ready ||
                                       s.Status == ToolChainStepStatus.Running);
        }

        private List<ToolChainStep> GetReadySteps(ToolChain chain)
        {
            var readySteps = new List<ToolChainStep>();

            foreach (var step in chain.Steps.Where(s => s.Status == ToolChainStepStatus.Pending))
            {
                // Check if all dependencies are completed
                bool allDependenciesMet = true;

                foreach (var depId in step.DependsOn)
                {
                    var depStep = chain.Steps.FirstOrDefault(s => s.Id == depId);
                    if (depStep == null || depStep.Status != ToolChainStepStatus.Completed)
                    {
                        allDependenciesMet = false;
                        break;
                    }
                }

                if (allDependenciesMet)
                {
                    step.Status = ToolChainStepStatus.Ready;
                    readySteps.Add(step);
                }
            }

            return readySteps;
        }

        #endregion

        #region Variable Resolution

        /// <summary>
        /// Resolve variable references in arguments
        /// </summary>
        private Dictionary<string, object> ResolveArguments(
            Dictionary<string, object> arguments,
            Dictionary<string, object> variables)
        {
            var resolved = new Dictionary<string, object>();

            foreach (var kvp in arguments)
            {
                resolved[kvp.Key] = ResolveValue(kvp.Value, variables);
            }

            return resolved;
        }

        private object ResolveValue(object value, Dictionary<string, object> variables)
        {
            // If value is a string starting with $, treat as variable reference
            if (value is string strValue && strValue.StartsWith("$"))
            {
                var varName = strValue.Substring(1);
                if (variables.TryGetValue(varName, out var varValue))
                {
                    return varValue;
                }
                
                _logger.LogWarning("Variable {Variable} not found, using literal value", varName);
                return strValue;
            }

            // If value is a dictionary, recursively resolve
            if (value is Dictionary<string, object> dictValue)
            {
                return ResolveArguments(dictValue, variables);
            }

            return value;
        }

        #endregion

        #region Chain Management

        /// <summary>
        /// Get chain by ID
        /// </summary>
        public ToolChain? GetChain(string chainId)
        {
            _activeChains.TryGetValue(chainId, out var chain);
            return chain;
        }

        /// <summary>
        /// Cancel a running chain
        /// </summary>
        public void CancelChain(string chainId)
        {
            if (_activeChains.TryGetValue(chainId, out var chain))
            {
                chain.Status = ToolChainStatus.Cancelled;
                chain.CompletedAt = DateTime.UtcNow;
                chain.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Chain cancelled");

                _logger.LogInformation("Cancelled chain {ChainId}", chainId);
            }
        }

        /// <summary>
        /// Get chain statistics
        /// </summary>
        public Dictionary<string, object> GetChainStatistics(string chainId)
        {
            if (!_activeChains.TryGetValue(chainId, out var chain))
            {
                return new Dictionary<string, object>();
            }

            var completedSteps = chain.Steps.Count(s => s.Status == ToolChainStepStatus.Completed);
            var failedSteps = chain.Steps.Count(s => s.Status == ToolChainStepStatus.Failed);
            var totalSteps = chain.Steps.Count;

            var executionTime = chain.CompletedAt.HasValue && chain.StartedAt.HasValue
                ? (chain.CompletedAt.Value - chain.StartedAt.Value).TotalSeconds
                : 0;

            return new Dictionary<string, object>
            {
                ["ChainId"] = chain.Id,
                ["Name"] = chain.Name,
                ["Status"] = chain.Status.ToString(),
                ["TotalSteps"] = totalSteps,
                ["CompletedSteps"] = completedSteps,
                ["FailedSteps"] = failedSteps,
                ["Progress"] = totalSteps > 0 ? (double)completedSteps / totalSteps : 0,
                ["ExecutionTime"] = executionTime,
                ["Variables"] = chain.Variables.Count
            };
        }

        /// <summary>
        /// Export chain execution log
        /// </summary>
        public string ExportChainLog(string chainId)
        {
            if (!_activeChains.TryGetValue(chainId, out var chain))
            {
                return "Chain not found";
            }

            var log = new System.Text.StringBuilder();
            log.AppendLine($"Tool Chain: {chain.Name} ({chain.Id})");
            log.AppendLine($"Status: {chain.Status}");
            log.AppendLine($"Created: {chain.CreatedAt:yyyy-MM-dd HH:mm:ss}");
            
            if (chain.StartedAt.HasValue)
                log.AppendLine($"Started: {chain.StartedAt.Value:yyyy-MM-dd HH:mm:ss}");
            
            if (chain.CompletedAt.HasValue)
                log.AppendLine($"Completed: {chain.CompletedAt.Value:yyyy-MM-dd HH:mm:ss}");

            log.AppendLine("\nExecution Log:");
            foreach (var entry in chain.ExecutionLog)
            {
                log.AppendLine(entry);
            }

            log.AppendLine("\nSteps:");
            foreach (var step in chain.Steps)
            {
                log.AppendLine($"\n  Step {step.ToolName} ({step.Id}):");
                log.AppendLine($"    Status: {step.Status}");
                
                if (step.StartTime.HasValue)
                    log.AppendLine($"    Started: {step.StartTime.Value:HH:mm:ss.fff}");
                
                if (step.EndTime.HasValue)
                {
                    log.AppendLine($"    Ended: {step.EndTime.Value:HH:mm:ss.fff}");
                    var duration = (step.EndTime.Value - step.StartTime!.Value).TotalMilliseconds;
                    log.AppendLine($"    Duration: {duration:F2}ms");
                }
                
                if (!string.IsNullOrEmpty(step.Error))
                    log.AppendLine($"    Error: {step.Error}");
            }

            return log.ToString();
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get global tool chaining statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["ActiveChains"] = _activeChains.Count,
                ["TotalSteps"] = _activeChains.Values.Sum(c => c.Steps.Count),
                ["CompletedChains"] = _activeChains.Values.Count(c => c.Status == ToolChainStatus.Completed),
                ["FailedChains"] = _activeChains.Values.Count(c => c.Status == ToolChainStatus.Failed),
                ["RunningChains"] = _activeChains.Values.Count(c => c.Status == ToolChainStatus.Running)
            };
        }

        #endregion
    }
}
