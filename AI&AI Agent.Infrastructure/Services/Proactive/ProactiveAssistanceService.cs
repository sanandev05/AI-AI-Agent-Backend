using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Proactive
{
    /// <summary>
    /// Context-aware suggestion model
    /// </summary>
    public class Suggestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public SuggestionType Type { get; set; }
        public double RelevanceScore { get; set; }
        public string? ActionCommand { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum SuggestionType
    {
        TaskBreakdown,
        NextStep,
        Optimization,
        BestPractice,
        Learning,
        Automation
    }

    /// <summary>
    /// Task with breakdown and progress
    /// </summary>
    public class TaskWithProgress
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<SubTask> SubTasks { get; set; } = new();
        public TaskStatus Status { get; set; }
        public double Progress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public List<string> Tags { get; set; } = new();
    }

    public class SubTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public TaskStatus Status { get; set; }
        public int EstimatedMinutes { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public enum TaskStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Blocked,
        Cancelled
    }

    /// <summary>
    /// Smart notification model
    /// </summary>
    public class SmartNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public NotificationPriority Priority { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public string? ActionUrl { get; set; }
    }

    public enum NotificationType
    {
        TaskReminder,
        ProgressUpdate,
        Suggestion,
        Warning,
        Success,
        Error
    }

    public enum NotificationPriority
    {
        Low,
        Normal,
        High,
        Urgent
    }

    /// <summary>
    /// Proactive assistance service for context-aware help
    /// </summary>
    public class ProactiveAssistanceService
    {
        private readonly ILogger<ProactiveAssistanceService> _logger;
        private readonly ConcurrentDictionary<string, List<Suggestion>> _userSuggestions = new();
        private readonly ConcurrentDictionary<string, TaskWithProgress> _tasks = new();
        private readonly ConcurrentDictionary<string, List<SmartNotification>> _notifications = new();
        private readonly ConcurrentDictionary<string, List<string>> _userContext = new();

        public ProactiveAssistanceService(ILogger<ProactiveAssistanceService> logger)
        {
            _logger = logger;
        }

        #region Context-Aware Suggestions

        public List<Suggestion> GenerateSuggestions(string userId, string currentContext, Dictionary<string, object>? metadata = null)
        {
            var suggestions = new List<Suggestion>();

            // Update user context
            if (!_userContext.ContainsKey(userId))
            {
                _userContext[userId] = new List<string>();
            }
            _userContext[userId].Add(currentContext);

            // Generate context-specific suggestions
            suggestions.AddRange(GenerateTaskSuggestions(userId, currentContext));
            suggestions.AddRange(GenerateOptimizationSuggestions(userId, currentContext));
            suggestions.AddRange(GenerateLearningSuggestions(userId, currentContext));

            // Store suggestions
            if (!_userSuggestions.ContainsKey(userId))
            {
                _userSuggestions[userId] = new List<Suggestion>();
            }
            _userSuggestions[userId].AddRange(suggestions);

            // Keep only recent suggestions
            _userSuggestions[userId] = _userSuggestions[userId]
                .OrderByDescending(s => s.CreatedAt)
                .Take(50)
                .ToList();

            _logger.LogInformation("Generated {Count} suggestions for user {UserId}", suggestions.Count, userId);
            return suggestions;
        }

        private List<Suggestion> GenerateTaskSuggestions(string userId, string context)
        {
            var suggestions = new List<Suggestion>();

            if (context.Contains("start") || context.Contains("begin"))
            {
                suggestions.Add(new Suggestion
                {
                    Title = "Break Down Task",
                    Description = "Consider breaking this task into smaller, manageable subtasks",
                    Type = SuggestionType.TaskBreakdown,
                    RelevanceScore = 0.8,
                    ActionCommand = "breakdown_task"
                });
            }

            if (context.Contains("code") || context.Contains("implement"))
            {
                suggestions.Add(new Suggestion
                {
                    Title = "Set Up Testing",
                    Description = "Consider setting up unit tests before implementation",
                    Type = SuggestionType.BestPractice,
                    RelevanceScore = 0.7,
                    ActionCommand = "setup_tests"
                });
            }

            return suggestions;
        }

        private List<Suggestion> GenerateOptimizationSuggestions(string userId, string context)
        {
            var suggestions = new List<Suggestion>();

            if (context.Contains("slow") || context.Contains("performance"))
            {
                suggestions.Add(new Suggestion
                {
                    Title = "Profile Performance",
                    Description = "Use profiling tools to identify bottlenecks",
                    Type = SuggestionType.Optimization,
                    RelevanceScore = 0.9,
                    ActionCommand = "profile_code"
                });
            }

            return suggestions;
        }

        private List<Suggestion> GenerateLearningSuggestions(string userId, string context)
        {
            var suggestions = new List<Suggestion>();

            if (context.Contains("error") || context.Contains("failed"))
            {
                suggestions.Add(new Suggestion
                {
                    Title = "Learn From Error",
                    Description = "Common patterns in similar errors suggest reviewing error handling",
                    Type = SuggestionType.Learning,
                    RelevanceScore = 0.6
                });
            }

            return suggestions;
        }

        public List<Suggestion> GetSuggestions(string userId)
        {
            return _userSuggestions.TryGetValue(userId, out var suggestions)
                ? suggestions.OrderByDescending(s => s.RelevanceScore).ToList()
                : new List<Suggestion>();
        }

        #endregion

        #region Automated Task Breakdown

        public TaskWithProgress BreakdownTask(string title, string description)
        {
            var task = new TaskWithProgress
            {
                Title = title,
                Description = description,
                Status = TaskStatus.NotStarted
            };

            // Automatic breakdown based on description
            task.SubTasks = GenerateSubTasks(description);

            _tasks[task.Id] = task;

            _logger.LogInformation("Created task {TaskId} with {SubTaskCount} subtasks", task.Id, task.SubTasks.Count);
            return task;
        }

        private List<SubTask> GenerateSubTasks(string description)
        {
            var subTasks = new List<SubTask>();

            // Simple heuristics for task breakdown
            if (description.Contains("API") || description.Contains("endpoint"))
            {
                subTasks.Add(new SubTask { Title = "Design API contract", EstimatedMinutes = 30 });
                subTasks.Add(new SubTask { Title = "Implement endpoint", EstimatedMinutes = 60 });
                subTasks.Add(new SubTask { Title = "Add validation", EstimatedMinutes = 20 });
                subTasks.Add(new SubTask { Title = "Write tests", EstimatedMinutes = 40 });
                subTasks.Add(new SubTask { Title = "Update documentation", EstimatedMinutes = 20 });
            }
            else if (description.Contains("feature") || description.Contains("component"))
            {
                subTasks.Add(new SubTask { Title = "Plan component structure", EstimatedMinutes = 20 });
                subTasks.Add(new SubTask { Title = "Implement core functionality", EstimatedMinutes = 90 });
                subTasks.Add(new SubTask { Title = "Add error handling", EstimatedMinutes = 30 });
                subTasks.Add(new SubTask { Title = "Test edge cases", EstimatedMinutes = 45 });
                subTasks.Add(new SubTask { Title = "Code review and refactor", EstimatedMinutes = 30 });
            }
            else
            {
                // Generic breakdown
                subTasks.Add(new SubTask { Title = "Research and plan", EstimatedMinutes = 30 });
                subTasks.Add(new SubTask { Title = "Implementation", EstimatedMinutes = 90 });
                subTasks.Add(new SubTask { Title = "Testing", EstimatedMinutes = 30 });
                subTasks.Add(new SubTask { Title = "Documentation", EstimatedMinutes = 15 });
            }

            return subTasks;
        }

        public void UpdateSubTaskStatus(string taskId, string subTaskId, TaskStatus status)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                var subTask = task.SubTasks.FirstOrDefault(st => st.Id == subTaskId);
                if (subTask != null)
                {
                    subTask.Status = status;
                    if (status == TaskStatus.Completed)
                    {
                        subTask.CompletedAt = DateTime.UtcNow;
                    }

                    // Update task progress
                    task.Progress = (double)task.SubTasks.Count(st => st.Status == TaskStatus.Completed) / task.SubTasks.Count;

                    if (task.Progress >= 1.0)
                    {
                        task.Status = TaskStatus.Completed;
                        task.CompletedAt = DateTime.UtcNow;
                    }

                    _logger.LogInformation("Updated subtask {SubTaskId} status to {Status}", subTaskId, status);
                }
            }
        }

        #endregion

        #region Progress Tracking

        public Dictionary<string, object> GetTaskProgress(string taskId)
        {
            if (!_tasks.TryGetValue(taskId, out var task))
            {
                return new Dictionary<string, object> { ["error"] = "Task not found" };
            }

            var completedSubTasks = task.SubTasks.Count(st => st.Status == TaskStatus.Completed);
            var totalEstimatedMinutes = task.SubTasks.Sum(st => st.EstimatedMinutes);
            var completedMinutes = task.SubTasks
                .Where(st => st.Status == TaskStatus.Completed)
                .Sum(st => st.EstimatedMinutes);

            return new Dictionary<string, object>
            {
                ["taskId"] = taskId,
                ["title"] = task.Title,
                ["status"] = task.Status.ToString(),
                ["progress"] = task.Progress,
                ["completedSubTasks"] = completedSubTasks,
                ["totalSubTasks"] = task.SubTasks.Count,
                ["estimatedTotalMinutes"] = totalEstimatedMinutes,
                ["completedMinutes"] = completedMinutes,
                ["remainingMinutes"] = totalEstimatedMinutes - completedMinutes,
                ["createdAt"] = task.CreatedAt,
                ["completedAt"] = task.CompletedAt
            };
        }

        public List<TaskWithProgress> GetActiveTasks()
        {
            return _tasks.Values
                .Where(t => t.Status == TaskStatus.InProgress || t.Status == TaskStatus.NotStarted)
                .OrderByDescending(t => t.CreatedAt)
                .ToList();
        }

        #endregion

        #region Smart Notifications

        public SmartNotification CreateNotification(
            string userId,
            string title,
            string message,
            NotificationType type,
            NotificationPriority priority = NotificationPriority.Normal,
            string? actionUrl = null)
        {
            var notification = new SmartNotification
            {
                Title = title,
                Message = message,
                Type = type,
                Priority = priority,
                ActionUrl = actionUrl
            };

            if (!_notifications.ContainsKey(userId))
            {
                _notifications[userId] = new List<SmartNotification>();
            }
            _notifications[userId].Add(notification);

            _logger.LogInformation("Created {Type} notification for user {UserId}", type, userId);
            return notification;
        }

        public void SendTaskReminder(string userId, string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                var inProgressSubTasks = task.SubTasks.Count(st => st.Status == TaskStatus.InProgress);
                var notStartedSubTasks = task.SubTasks.Count(st => st.Status == TaskStatus.NotStarted);

                if (inProgressSubTasks > 0 || notStartedSubTasks > 0)
                {
                    CreateNotification(
                        userId,
                        "Task Reminder",
                        $"Task '{task.Title}' has {inProgressSubTasks} in-progress and {notStartedSubTasks} pending subtasks",
                        NotificationType.TaskReminder,
                        NotificationPriority.Normal
                    );
                }
            }
        }

        public void SendProgressUpdate(string userId, string taskId)
        {
            if (_tasks.TryGetValue(taskId, out var task))
            {
                CreateNotification(
                    userId,
                    "Progress Update",
                    $"Task '{task.Title}' is {task.Progress:P0} complete",
                    NotificationType.ProgressUpdate,
                    task.Progress > 0.8 ? NotificationPriority.High : NotificationPriority.Normal
                );
            }
        }

        public List<SmartNotification> GetNotifications(string userId, bool unreadOnly = false)
        {
            if (!_notifications.TryGetValue(userId, out var notifications))
            {
                return new List<SmartNotification>();
            }

            return unreadOnly
                ? notifications.Where(n => !n.IsRead).OrderByDescending(n => n.CreatedAt).ToList()
                : notifications.OrderByDescending(n => n.CreatedAt).ToList();
        }

        public void MarkNotificationAsRead(string userId, string notificationId)
        {
            if (_notifications.TryGetValue(userId, out var notifications))
            {
                var notification = notifications.FirstOrDefault(n => n.Id == notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    _logger.LogDebug("Marked notification {NotificationId} as read", notificationId);
                }
            }
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics(string userId)
        {
            var tasks = _tasks.Values.ToList();
            var suggestions = GetSuggestions(userId);
            var notifications = GetNotifications(userId);

            return new Dictionary<string, object>
            {
                ["totalTasks"] = tasks.Count,
                ["activeTasks"] = tasks.Count(t => t.Status == TaskStatus.InProgress),
                ["completedTasks"] = tasks.Count(t => t.Status == TaskStatus.Completed),
                ["totalSuggestions"] = suggestions.Count,
                ["highRelevanceSuggestions"] = suggestions.Count(s => s.RelevanceScore > 0.7),
                ["totalNotifications"] = notifications.Count,
                ["unreadNotifications"] = notifications.Count(n => !n.IsRead),
                ["urgentNotifications"] = notifications.Count(n => n.Priority == NotificationPriority.Urgent)
            };
        }

        #endregion
    }
}
