namespace AI_AI_Agent.Domain.Security
{
    /// <summary>
    /// Represents a security validation result
    /// </summary>
    public class SecurityValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Violations { get; set; } = new();
        public SecurityRiskLevel RiskLevel { get; set; } = SecurityRiskLevel.Low;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public enum SecurityRiskLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Represents a rate limit configuration
    /// </summary>
    public class RateLimitConfig
    {
        public string Resource { get; set; } = string.Empty;
        public int MaxRequests { get; set; }
        public TimeSpan TimeWindow { get; set; }
        public string? UserId { get; set; }
        public Dictionary<string, int> TokenBudgets { get; set; } = new();
    }

    /// <summary>
    /// Represents a sandbox execution context
    /// </summary>
    public class SandboxContext
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public List<string> AllowedOperations { get; set; } = new();
        public List<string> AllowedPaths { get; set; } = new();
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public long MaxMemoryBytes { get; set; } = 512 * 1024 * 1024; // 512MB
        public bool NetworkAccess { get; set; } = false;
    }
}
