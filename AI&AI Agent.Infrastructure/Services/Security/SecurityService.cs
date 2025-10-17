using AI_AI_Agent.Domain.Security;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AI_AI_Agent.Infrastructure.Services.Security
{
    /// <summary>
    /// Comprehensive security service for input validation, rate limiting, and sandboxing
    /// </summary>
    public class SecurityService
    {
        private readonly ILogger<SecurityService> _logger;
        private readonly ConcurrentDictionary<string, List<DateTime>> _rateLimitTracking = new();
        private readonly ConcurrentDictionary<string, int> _tokenUsage = new();
        private readonly List<string> _blockedPatterns = new();
        private readonly List<string> _dangerousKeywords = new()
        {
            "eval", "exec", "system", "rm -rf", "DROP TABLE", "DELETE FROM",
            "__import__", "subprocess", "os.system", "powershell", "cmd.exe"
        };

        public SecurityService(ILogger<SecurityService> logger)
        {
            _logger = logger;
            InitializeBlockedPatterns();
        }

        #region Input Validation

        /// <summary>
        /// Validate user input for security threats
        /// </summary>
        public SecurityValidationResult ValidateInput(string input, string context = "general")
        {
            var result = new SecurityValidationResult { IsValid = true };

            // Check for SQL injection patterns
            if (ContainsSqlInjection(input))
            {
                result.IsValid = false;
                result.Violations.Add("Potential SQL injection detected");
                result.RiskLevel = SecurityRiskLevel.High;
            }

            // Check for command injection
            if (ContainsCommandInjection(input))
            {
                result.IsValid = false;
                result.Violations.Add("Potential command injection detected");
                result.RiskLevel = SecurityRiskLevel.Critical;
            }

            // Check for path traversal
            if (ContainsPathTraversal(input))
            {
                result.IsValid = false;
                result.Violations.Add("Path traversal attempt detected");
                result.RiskLevel = SecurityRiskLevel.High;
            }

            // Check for dangerous keywords
            var dangerousKeyword = _dangerousKeywords.FirstOrDefault(kw =>
                input.Contains(kw, StringComparison.OrdinalIgnoreCase));
            if (dangerousKeyword != null)
            {
                result.IsValid = false;
                result.Violations.Add($"Dangerous keyword detected: {dangerousKeyword}");
                result.RiskLevel = SecurityRiskLevel.High;
            }

            // Check against blocked patterns
            foreach (var pattern in _blockedPatterns)
            {
                if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
                {
                    result.IsValid = false;
                    result.Violations.Add($"Input matches blocked pattern");
                    result.RiskLevel = SecurityRiskLevel.Medium;
                    break;
                }
            }

            // Check input length
            if (input.Length > 50000)
            {
                result.IsValid = false;
                result.Violations.Add("Input exceeds maximum length");
                result.RiskLevel = SecurityRiskLevel.Medium;
            }

            if (!result.IsValid)
            {
                _logger.LogWarning(
                    "Input validation failed for context {Context}: {Violations}",
                    context, string.Join(", ", result.Violations));
            }

            return result;
        }

        /// <summary>
        /// Validate file path for safe access
        /// </summary>
        public SecurityValidationResult ValidateFilePath(string path, List<string> allowedPaths)
        {
            var result = new SecurityValidationResult { IsValid = true };

            try
            {
                var fullPath = Path.GetFullPath(path);

                // Check if path is within allowed directories
                bool isAllowed = allowedPaths.Any(allowedPath =>
                    fullPath.StartsWith(Path.GetFullPath(allowedPath), StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    result.IsValid = false;
                    result.Violations.Add("Path is outside allowed directories");
                    result.RiskLevel = SecurityRiskLevel.High;
                }

                // Check for path traversal
                if (path.Contains("..") || path.Contains("~"))
                {
                    result.IsValid = false;
                    result.Violations.Add("Path traversal detected");
                    result.RiskLevel = SecurityRiskLevel.High;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Violations.Add($"Invalid path: {ex.Message}");
                result.RiskLevel = SecurityRiskLevel.Medium;
            }

            return result;
        }

        /// <summary>
        /// Sanitize user input by removing dangerous content
        /// </summary>
        public string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove control characters
            input = Regex.Replace(input, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

            // Escape HTML special characters
            input = input
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#x27;");

            return input;
        }

        private bool ContainsSqlInjection(string input)
        {
            var sqlPatterns = new[]
            {
                @"(\bOR\b|\bAND\b)\s+\d+\s*=\s*\d+",
                @"('|\b)(OR|AND)(\b|')\s+('|')=('|')",
                @";\s*(DROP|DELETE|INSERT|UPDATE|CREATE|ALTER)\s+",
                @"--\s*$",
                @"/\*.*\*/",
                @"\bUNION\b.*\bSELECT\b"
            };

            return sqlPatterns.Any(pattern =>
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        private bool ContainsCommandInjection(string input)
        {
            var commandPatterns = new[]
            {
                @"[;&|`$]",
                @"\$\(.*\)",
                @"`.*`",
                @">\s*/dev/",
                @"2>&1"
            };

            return commandPatterns.Any(pattern =>
                Regex.IsMatch(input, pattern));
        }

        private bool ContainsPathTraversal(string input)
        {
            return input.Contains("../") || input.Contains("..\\") ||
                   input.Contains("%2e%2e") || input.Contains("..%2f");
        }

        private void InitializeBlockedPatterns()
        {
            _blockedPatterns.AddRange(new[]
            {
                @"<script[^>]*>.*?</script>",
                @"javascript:",
                @"on\w+\s*=",
                @"data:text/html"
            });
        }

        #endregion

        #region Rate Limiting

        /// <summary>
        /// Check if request is within rate limits
        /// </summary>
        public bool CheckRateLimit(string userId, string resource, RateLimitConfig config)
        {
            var key = $"{userId}:{resource}";
            var now = DateTime.UtcNow;

            var requests = _rateLimitTracking.GetOrAdd(key, _ => new List<DateTime>());

            lock (requests)
            {
                // Remove old requests outside time window
                requests.RemoveAll(time => now - time > config.TimeWindow);

                // Check if under limit
                if (requests.Count >= config.MaxRequests)
                {
                    _logger.LogWarning(
                        "Rate limit exceeded for user {UserId} on resource {Resource}",
                        userId, resource);
                    return false;
                }

                // Add current request
                requests.Add(now);
            }

            return true;
        }

        /// <summary>
        /// Check token budget
        /// </summary>
        public bool CheckTokenBudget(string userId, string model, int tokensRequested)
        {
            var key = $"{userId}:{model}";
            var currentUsage = _tokenUsage.GetOrAdd(key, 0);

            // Default budget: 100k tokens per user per model per day
            const int DEFAULT_BUDGET = 100000;

            if (currentUsage + tokensRequested > DEFAULT_BUDGET)
            {
                _logger.LogWarning(
                    "Token budget exceeded for user {UserId} on model {Model}. Current: {Current}, Requested: {Requested}",
                    userId, model, currentUsage, tokensRequested);
                return false;
            }

            _tokenUsage[key] = currentUsage + tokensRequested;
            return true;
        }

        /// <summary>
        /// Reset rate limits for a user
        /// </summary>
        public void ResetRateLimits(string userId)
        {
            var keysToRemove = _rateLimitTracking.Keys
                .Where(key => key.StartsWith($"{userId}:"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _rateLimitTracking.TryRemove(key, out _);
            }

            _logger.LogInformation("Reset rate limits for user {UserId}", userId);
        }

        /// <summary>
        /// Get rate limit status
        /// </summary>
        public Dictionary<string, object> GetRateLimitStatus(string userId, string resource)
        {
            var key = $"{userId}:{resource}";
            var requests = _rateLimitTracking.GetOrAdd(key, _ => new List<DateTime>());

            lock (requests)
            {
                var now = DateTime.UtcNow;
                var recentRequests = requests.Count(time => now - time <= TimeSpan.FromMinutes(1));

                return new Dictionary<string, object>
                {
                    ["UserId"] = userId,
                    ["Resource"] = resource,
                    ["RequestsLastMinute"] = recentRequests,
                    ["TotalTracked"] = requests.Count
                };
            }
        }

        #endregion

        #region Sandboxing

        /// <summary>
        /// Create a sandbox context for isolated execution
        /// </summary>
        public SandboxContext CreateSandbox(string userId, List<string>? allowedPaths = null)
        {
            var sandbox = new SandboxContext
            {
                UserId = userId,
                AllowedPaths = allowedPaths ?? new List<string>(),
                AllowedOperations = new List<string> { "read", "write" }
            };

            _logger.LogInformation("Created sandbox {SandboxId} for user {UserId}", sandbox.Id, userId);

            return sandbox;
        }

        /// <summary>
        /// Execute code in sandbox (placeholder for actual sandboxing implementation)
        /// </summary>
        public async Task<(bool Success, string Output, string Error)> ExecuteInSandbox(
            SandboxContext context,
            string code,
            string language)
        {
            _logger.LogInformation(
                "Executing {Language} code in sandbox {SandboxId}",
                language, context.Id);

            // Validate code before execution
            var validation = ValidateInput(code, $"sandbox_{language}");
            if (!validation.IsValid)
            {
                return (false, string.Empty, $"Code validation failed: {string.Join(", ", validation.Violations)}");
            }

            try
            {
                // This is a placeholder - actual implementation would use:
                // - Docker containers for isolation
                // - Process-level sandboxing with resource limits
                // - Virtual machines for high-risk code
                
                // For now, return a safe response
                _logger.LogWarning(
                    "Sandbox execution requested but not fully implemented. Code validation passed.");

                return (true, "Sandbox execution placeholder - implement with Docker/containers", string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing code in sandbox {SandboxId}", context.Id);
                return (false, string.Empty, ex.Message);
            }
        }

        /// <summary>
        /// Validate operation is allowed in sandbox
        /// </summary>
        public bool ValidateSandboxOperation(SandboxContext context, string operation, string? path = null)
        {
            // Check if operation is allowed
            if (!context.AllowedOperations.Contains(operation, StringComparer.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Operation {Operation} not allowed in sandbox {SandboxId}",
                    operation, context.Id);
                return false;
            }

            // If path is provided, validate it
            if (!string.IsNullOrEmpty(path))
            {
                var pathValidation = ValidateFilePath(path, context.AllowedPaths);
                if (!pathValidation.IsValid)
                {
                    _logger.LogWarning(
                        "Path {Path} not allowed in sandbox {SandboxId}: {Violations}",
                        path, context.Id, string.Join(", ", pathValidation.Violations));
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Security Monitoring

        /// <summary>
        /// Get security statistics
        /// </summary>
        public Dictionary<string, object> GetSecurityStatistics()
        {
            return new Dictionary<string, object>
            {
                ["TrackedUsers"] = _rateLimitTracking.Keys.Select(k => k.Split(':')[0]).Distinct().Count(),
                ["TotalRateLimitEntries"] = _rateLimitTracking.Count,
                ["TotalTokenUsageEntries"] = _tokenUsage.Count,
                ["BlockedPatternsCount"] = _blockedPatterns.Count,
                ["DangerousKeywordsCount"] = _dangerousKeywords.Count
            };
        }

        /// <summary>
        /// Add custom blocked pattern
        /// </summary>
        public void AddBlockedPattern(string pattern)
        {
            if (!_blockedPatterns.Contains(pattern))
            {
                _blockedPatterns.Add(pattern);
                _logger.LogInformation("Added blocked pattern: {Pattern}", pattern);
            }
        }

        /// <summary>
        /// Remove blocked pattern
        /// </summary>
        public void RemoveBlockedPattern(string pattern)
        {
            if (_blockedPatterns.Remove(pattern))
            {
                _logger.LogInformation("Removed blocked pattern: {Pattern}", pattern);
            }
        }

        #endregion
    }
}
