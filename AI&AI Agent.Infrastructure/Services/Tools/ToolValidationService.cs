using AI_AI_Agent.Domain.Tools;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AI_AI_Agent.Infrastructure.Services.Tools
{
    /// <summary>
    /// Tool validation service for argument validation, sanitization, and safety checks
    /// </summary>
    public class ToolValidationService
    {
        private readonly ILogger<ToolValidationService> _logger;
        private readonly HashSet<string> _dangerousPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            // Command injection patterns
            ";", "&&", "||", "|", "`", "$(",
            // SQL injection patterns
            "DROP", "DELETE", "TRUNCATE", "EXEC", "EXECUTE",
            // Path traversal
            "..", "~",
            // Script injection
            "<script", "javascript:", "onerror=", "onload="
        };

        private readonly HashSet<string> _allowedProtocols = new(StringComparer.OrdinalIgnoreCase)
        {
            "http", "https", "ftp", "ftps"
        };

        public ToolValidationService(ILogger<ToolValidationService> logger)
        {
            _logger = logger;
        }

        #region Argument Validation

        /// <summary>
        /// Validate tool arguments before execution
        /// </summary>
        public ValidationResult ValidateArguments(
            string toolName,
            Dictionary<string, object> arguments,
            ToolMetadata metadata)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Check required capabilities
                if (metadata.RequiredCapabilities.Any())
                {
                    // This would be checked against user/agent permissions
                    _logger.LogDebug("Tool {Tool} requires capabilities: {Capabilities}",
                        toolName, string.Join(", ", metadata.RequiredCapabilities));
                }

                // Validate each argument
                foreach (var arg in arguments)
                {
                    var argValidation = ValidateArgument(toolName, arg.Key, arg.Value);
                    if (!argValidation.IsValid)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"{arg.Key}: {argValidation.Message}");
                    }
                }

                // Check for dangerous patterns
                var dangerousCheck = CheckForDangerousPatterns(arguments);
                if (!dangerousCheck.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(dangerousCheck.Errors);
                }

                // Validate URLs
                var urlValidation = ValidateUrls(arguments);
                if (!urlValidation.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(urlValidation.Errors);
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Validation error: {ex.Message}");
                _logger.LogError(ex, "Error validating arguments for tool {Tool}", toolName);
            }

            return result;
        }

        /// <summary>
        /// Validate a single argument
        /// </summary>
        private ValidationResult ValidateArgument(string toolName, string argName, object value)
        {
            var result = new ValidationResult { IsValid = true };

            if (value == null)
            {
                result.IsValid = false;
                result.Message = "Argument cannot be null";
                return result;
            }

            // String validation
            if (value is string strValue)
            {
                // Check max length
                if (strValue.Length > 100000) // 100KB limit
                {
                    result.IsValid = false;
                    result.Message = "String too long (max 100,000 characters)";
                    return result;
                }

                // Check for null bytes
                if (strValue.Contains('\0'))
                {
                    result.IsValid = false;
                    result.Message = "String contains null bytes";
                    return result;
                }
            }

            // Number validation
            if (value is int intValue)
            {
                if (intValue < 0 && argName.Contains("count", StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Message = "Count cannot be negative";
                    return result;
                }
            }

            return result;
        }

        #endregion

        #region Input Sanitization

        /// <summary>
        /// Sanitize tool arguments to prevent injection attacks
        /// </summary>
        public Dictionary<string, object> SanitizeArguments(Dictionary<string, object> arguments)
        {
            var sanitized = new Dictionary<string, object>();

            foreach (var arg in arguments)
            {
                sanitized[arg.Key] = SanitizeValue(arg.Value);
            }

            return sanitized;
        }

        /// <summary>
        /// Sanitize a single value
        /// </summary>
        private object SanitizeValue(object value)
        {
            if (value is string strValue)
            {
                // Remove null bytes
                strValue = strValue.Replace("\0", "");

                // Trim whitespace
                strValue = strValue.Trim();

                // Normalize line endings
                strValue = strValue.Replace("\r\n", "\n");

                // Remove control characters (except newline and tab)
                strValue = Regex.Replace(strValue, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

                return strValue;
            }

            if (value is Dictionary<string, object> dictValue)
            {
                return SanitizeArguments(dictValue);
            }

            if (value is List<object> listValue)
            {
                return listValue.Select(SanitizeValue).ToList();
            }

            return value;
        }

        #endregion

        #region Safety Checks

        /// <summary>
        /// Check for dangerous patterns in arguments
        /// </summary>
        private ValidationResult CheckForDangerousPatterns(Dictionary<string, object> arguments)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var arg in arguments)
            {
                if (arg.Value is string strValue)
                {
                    foreach (var pattern in _dangerousPatterns)
                    {
                        if (strValue.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Potentially dangerous pattern detected in {arg.Key}: {pattern}");
                            _logger.LogWarning("Dangerous pattern {Pattern} detected in argument {Arg}",
                                pattern, arg.Key);
                        }
                    }

                    // Check for SQL injection patterns
                    if (ContainsSqlInjection(strValue))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Potential SQL injection detected in {arg.Key}");
                        _logger.LogWarning("Potential SQL injection in argument {Arg}", arg.Key);
                    }

                    // Check for command injection
                    if (ContainsCommandInjection(strValue))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Potential command injection detected in {arg.Key}");
                        _logger.LogWarning("Potential command injection in argument {Arg}", arg.Key);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Validate URLs in arguments
        /// </summary>
        private ValidationResult ValidateUrls(Dictionary<string, object> arguments)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var arg in arguments)
            {
                if (arg.Key.Contains("url", StringComparison.OrdinalIgnoreCase) && arg.Value is string urlStr)
                {
                    if (!IsValidUrl(urlStr))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Invalid URL in {arg.Key}: {urlStr}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Check if URL is valid and safe
        /// </summary>
        private bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            // Check protocol
            if (!_allowedProtocols.Contains(uri.Scheme))
            {
                _logger.LogWarning("Disallowed protocol: {Scheme}", uri.Scheme);
                return false;
            }

            // Block localhost and private IPs
            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Equals("127.0.0.1") ||
                uri.Host.StartsWith("192.168.") ||
                uri.Host.StartsWith("10.") ||
                uri.Host.StartsWith("172."))
            {
                _logger.LogWarning("Blocked private/local URL: {Host}", uri.Host);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check for SQL injection patterns
        /// </summary>
        private bool ContainsSqlInjection(string input)
        {
            var sqlPatterns = new[]
            {
                @"(\bOR\b|\bAND\b)\s+[\d\w]+\s*=\s*[\d\w]+",
                @"'\s*(OR|AND)\s*'",
                @"--",
                @"/\*.*\*/",
                @"xp_",
                @"sp_",
                @"UNION\s+SELECT",
                @";\s*DROP",
                @";\s*DELETE",
                @";\s*TRUNCATE"
            };

            return sqlPatterns.Any(pattern =>
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Check for command injection patterns
        /// </summary>
        private bool ContainsCommandInjection(string input)
        {
            var commandPatterns = new[]
            {
                @";\s*rm\s+",
                @";\s*del\s+",
                @";\s*cat\s+",
                @"\$\(.*\)",
                @"`.*`",
                @">\s*/dev/",
                @"\|\s*sh",
                @"\|\s*bash",
                @"&&\s*rm",
                @"\|\|\s*rm"
            };

            return commandPatterns.Any(pattern =>
                Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase));
        }

        #endregion

        #region Result Validation

        /// <summary>
        /// Validate tool execution result
        /// </summary>
        public ValidationResult ValidateResult(string toolName, object result, ToolMetadata metadata)
        {
            var validationResult = new ValidationResult { IsValid = true };

            try
            {
                if (result == null)
                {
                    validationResult.IsValid = false;
                    validationResult.Errors.Add("Tool returned null result");
                    return validationResult;
                }

                // Check result size
                var resultJson = JsonSerializer.Serialize(result);
                if (resultJson.Length > 10_000_000) // 10MB limit
                {
                    validationResult.IsValid = false;
                    validationResult.Errors.Add("Result too large (max 10MB)");
                    return validationResult;
                }

                // Validate result structure
                if (result is string strResult)
                {
                    // Check for error indicators
                    if (strResult.Contains("error", StringComparison.OrdinalIgnoreCase) &&
                        strResult.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    {
                        validationResult.Warnings.Add("Result contains error indicators");
                    }
                }

                _logger.LogDebug("Validated result for tool {Tool}, size: {Size}",
                    toolName, resultJson.Length);
            }
            catch (Exception ex)
            {
                validationResult.IsValid = false;
                validationResult.Errors.Add($"Result validation error: {ex.Message}");
                _logger.LogError(ex, "Error validating result for tool {Tool}", toolName);
            }

            return validationResult;
        }

        #endregion

        #region File Path Validation

        /// <summary>
        /// Validate file paths to prevent path traversal
        /// </summary>
        public ValidationResult ValidateFilePath(string filePath, string allowedBasePath)
        {
            var result = new ValidationResult { IsValid = true };

            try
            {
                // Normalize path
                var normalizedPath = Path.GetFullPath(filePath);
                var normalizedBase = Path.GetFullPath(allowedBasePath);

                // Check if path is within allowed directory
                if (!normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                {
                    result.IsValid = false;
                    result.Errors.Add("Path traversal detected: path outside allowed directory");
                    _logger.LogWarning("Path traversal attempt: {Path} not in {Base}",
                        normalizedPath, normalizedBase);
                }

                // Check for suspicious patterns
                if (filePath.Contains("..") || filePath.Contains("~"))
                {
                    result.IsValid = false;
                    result.Errors.Add("Suspicious path pattern detected");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Errors.Add($"Path validation error: {ex.Message}");
                _logger.LogError(ex, "Error validating file path {Path}", filePath);
            }

            return result;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get validation statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["DangerousPatterns"] = _dangerousPatterns.Count,
                ["AllowedProtocols"] = _allowedProtocols.Count,
                ["MaxArgumentLength"] = 100000,
                ["MaxResultSize"] = 10_000_000
            };
        }

        #endregion
    }

    /// <summary>
    /// Validation result
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
