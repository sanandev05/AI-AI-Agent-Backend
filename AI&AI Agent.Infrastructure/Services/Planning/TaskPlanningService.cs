using AI_AI_Agent.Domain.Planning;
using AI_AI_Agent.Domain.Agents;

namespace AI_AI_Agent.Infrastructure.Services.Planning
{
    /// <summary>
    /// Advanced task planning system with goal decomposition and dependency management
    /// </summary>
    public class TaskPlanningService
    {
        private readonly Dictionary<string, ExecutionPlan> _activePlans = new();

        /// <summary>
        /// Create an execution plan from a high-level goal
        /// </summary>
        public ExecutionPlan CreatePlan(string goal, string userContext = "")
        {
            var plan = new ExecutionPlan
            {
                Goal = goal,
                Status = ExecutionPlanStatus.Created
            };

            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Plan created for goal: {goal}");
            
            _activePlans[plan.Id] = plan;
            return plan;
        }

        /// <summary>
        /// Add a task to the execution plan
        /// </summary>
        public void AddTask(ExecutionPlan plan, PlanTask task)
        {
            plan.Tasks.Add(task);
            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task added: {task.Description} (Agent: {task.RequiredAgentType})");
        }

        /// <summary>
        /// Build dependency graph and determine execution order
        /// </summary>
        public List<List<PlanTask>> BuildDependencyGraph(ExecutionPlan plan)
        {
            var layers = new List<List<PlanTask>>();
            var taskDict = plan.Tasks.ToDictionary(t => t.Id);
            var processed = new HashSet<string>();

            // Build layers based on dependencies
            while (processed.Count < plan.Tasks.Count)
            {
                var currentLayer = new List<PlanTask>();

                foreach (var task in plan.Tasks.Where(t => !processed.Contains(t.Id)))
                {
                    // Check if all dependencies are satisfied
                    if (task.Dependencies.All(dep => processed.Contains(dep)))
                    {
                        currentLayer.Add(task);
                    }
                }

                if (currentLayer.Count == 0 && processed.Count < plan.Tasks.Count)
                {
                    // Circular dependency detected
                    throw new InvalidOperationException("Circular dependency detected in task plan");
                }

                // Sort by priority within layer
                currentLayer = currentLayer.OrderBy(t => t.Priority).ToList();
                layers.Add(currentLayer);

                foreach (var task in currentLayer)
                {
                    processed.Add(task.Id);
                }
            }

            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Dependency graph built: {layers.Count} execution layers");
            return layers;
        }

        /// <summary>
        /// Get tasks that are ready to execute (dependencies satisfied)
        /// </summary>
        public List<PlanTask> GetReadyTasks(ExecutionPlan plan)
        {
            var completedTaskIds = plan.Tasks
                .Where(t => t.Status == PlanTaskStatus.Completed)
                .Select(t => t.Id)
                .ToHashSet();

            return plan.Tasks
                .Where(t => t.Status == PlanTaskStatus.Pending || t.Status == PlanTaskStatus.Blocked)
                .Where(t => t.Dependencies.All(dep => completedTaskIds.Contains(dep)))
                .OrderBy(t => t.Priority)
                .ToList();
        }

        /// <summary>
        /// Mark a task as started
        /// </summary>
        public void StartTask(ExecutionPlan plan, PlanTask task)
        {
            task.Status = PlanTaskStatus.Running;
            task.StartTime = DateTime.UtcNow;
            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task started: {task.Description}");

            if (plan.Status == ExecutionPlanStatus.Created)
            {
                plan.Status = ExecutionPlanStatus.Running;
                plan.StartedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Mark a task as completed
        /// </summary>
        public void CompleteTask(ExecutionPlan plan, PlanTask task, string result)
        {
            task.Status = PlanTaskStatus.Completed;
            task.Result = result;
            task.EndTime = DateTime.UtcNow;
            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task completed: {task.Description}");

            // Check if plan is complete
            if (plan.Tasks.All(t => t.Status == PlanTaskStatus.Completed || t.Status == PlanTaskStatus.Skipped))
            {
                plan.Status = ExecutionPlanStatus.Completed;
                plan.CompletedAt = DateTime.UtcNow;
                plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Plan completed successfully");
            }
        }

        /// <summary>
        /// Mark a task as failed and determine retry strategy
        /// </summary>
        public bool FailTask(ExecutionPlan plan, PlanTask task, string error)
        {
            task.Error = error;
            task.EndTime = DateTime.UtcNow;
            task.RetryCount++;

            if (task.RetryCount < task.MaxRetries)
            {
                // Retry
                task.Status = PlanTaskStatus.Pending;
                task.StartTime = null;
                task.EndTime = null;
                plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task failed, retry {task.RetryCount}/{task.MaxRetries}: {task.Description} - {error}");
                return true; // Can retry
            }
            else
            {
                // Max retries exceeded
                task.Status = PlanTaskStatus.Failed;
                plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task failed permanently: {task.Description} - {error}");
                return false; // Cannot retry
            }
        }

        /// <summary>
        /// Replan dynamically when a task fails
        /// </summary>
        public void ReplanOnFailure(ExecutionPlan plan, PlanTask failedTask)
        {
            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Replanning due to task failure: {failedTask.Description}");

            // Find alternative approach
            // For now, skip dependent tasks
            var dependentTasks = plan.Tasks.Where(t => t.Dependencies.Contains(failedTask.Id)).ToList();
            
            foreach (var depTask in dependentTasks)
            {
                if (depTask.Status != PlanTaskStatus.Completed)
                {
                    depTask.Status = PlanTaskStatus.Skipped;
                    plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task skipped due to dependency failure: {depTask.Description}");
                }
            }
        }

        /// <summary>
        /// Prioritize tasks dynamically based on context
        /// </summary>
        public void ReprioritizeTasks(ExecutionPlan plan, Func<PlanTask, int> priorityFunction)
        {
            foreach (var task in plan.Tasks.Where(t => t.Status == PlanTaskStatus.Pending || t.Status == PlanTaskStatus.Blocked))
            {
                var oldPriority = task.Priority;
                task.Priority = priorityFunction(task);
                
                if (oldPriority != task.Priority)
                {
                    plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Task priority changed: {task.Description} ({oldPriority} → {task.Priority})");
                }
            }
        }

        /// <summary>
        /// Add context to the plan that can be shared between tasks
        /// </summary>
        public void AddContext(ExecutionPlan plan, string key, string value)
        {
            plan.Context[key] = value;
        }

        /// <summary>
        /// Get a plan by ID
        /// </summary>
        public ExecutionPlan? GetPlan(string planId)
        {
            _activePlans.TryGetValue(planId, out var plan);
            return plan;
        }

        /// <summary>
        /// Cancel an execution plan
        /// </summary>
        public void CancelPlan(ExecutionPlan plan)
        {
            plan.Status = ExecutionPlanStatus.Cancelled;
            plan.CompletedAt = DateTime.UtcNow;
            
            // Cancel all running/pending tasks
            foreach (var task in plan.Tasks.Where(t => t.Status == PlanTaskStatus.Running || t.Status == PlanTaskStatus.Pending))
            {
                task.Status = PlanTaskStatus.Skipped;
            }

            plan.ExecutionLog.Add($"[{DateTime.UtcNow:HH:mm:ss}] Plan cancelled");
        }

        /// <summary>
        /// Get execution statistics
        /// </summary>
        public Dictionary<string, object> GetPlanStatistics(ExecutionPlan plan)
        {
            var duration = plan.CompletedAt.HasValue && plan.StartedAt.HasValue
                ? (plan.CompletedAt.Value - plan.StartedAt.Value).TotalSeconds
                : 0;

            return new Dictionary<string, object>
            {
                { "TotalTasks", plan.TotalTasks },
                { "CompletedTasks", plan.CompletedTasks },
                { "FailedTasks", plan.FailedTasks },
                { "Progress", plan.Progress },
                { "Status", plan.Status.ToString() },
                { "DurationSeconds", duration },
                { "AverageTaskTime", plan.Tasks.Where(t => t.EndTime.HasValue && t.StartTime.HasValue)
                    .Select(t => (t.EndTime!.Value - t.StartTime!.Value).TotalSeconds)
                    .DefaultIfEmpty(0)
                    .Average() }
            };
        }
    }
}
