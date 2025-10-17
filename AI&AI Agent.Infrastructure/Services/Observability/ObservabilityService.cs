using AI_AI_Agent.Domain.Observability;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace AI_AI_Agent.Infrastructure.Services.Observability
{
    /// <summary>
    /// Comprehensive observability service for logging, tracing, and metrics
    /// </summary>
    public class ObservabilityService
    {
        private readonly ILogger<ObservabilityService> _logger;
        private readonly ConcurrentDictionary<string, ExecutionTrace> _activeTraces = new();
        private readonly ConcurrentDictionary<string, List<MetricDataPoint>> _metrics = new();
        private readonly ConcurrentQueue<string> _auditLog = new();
        private const int MAX_AUDIT_LOG_SIZE = 10000;

        public ObservabilityService(ILogger<ObservabilityService> logger)
        {
            _logger = logger;
        }

        #region Tracing

        /// <summary>
        /// Start a new execution trace
        /// </summary>
        public ExecutionTrace StartTrace(string agentId, string operation, Dictionary<string, string>? tags = null)
        {
            var trace = new ExecutionTrace
            {
                AgentId = agentId,
                Operation = operation,
                StartTime = DateTime.UtcNow,
                Tags = tags ?? new()
            };

            _activeTraces[trace.Id] = trace;

            _logger.LogInformation(
                "Started trace {TraceId} for agent {AgentId} operation {Operation}",
                trace.Id, agentId, operation);

            LogAudit($"TRACE_START|{trace.Id}|{agentId}|{operation}");

            return trace;
        }

        /// <summary>
        /// Add a span to an existing trace
        /// </summary>
        public ExecutionSpan AddSpan(string traceId, string name, string? parentSpanId = null)
        {
            if (!_activeTraces.TryGetValue(traceId, out var trace))
            {
                _logger.LogWarning("Attempted to add span to non-existent trace {TraceId}", traceId);
                return new ExecutionSpan { Name = name };
            }

            var span = new ExecutionSpan
            {
                Name = name,
                ParentId = parentSpanId ?? string.Empty,
                StartTime = DateTime.UtcNow
            };

            trace.Spans.Add(span);

            _logger.LogDebug("Added span {SpanName} to trace {TraceId}", name, traceId);

            return span;
        }

        /// <summary>
        /// End a span
        /// </summary>
        public void EndSpan(string traceId, string spanId)
        {
            if (!_activeTraces.TryGetValue(traceId, out var trace))
                return;

            var span = trace.Spans.FirstOrDefault(s => s.Id == spanId);
            if (span != null)
            {
                span.EndTime = DateTime.UtcNow;
                _logger.LogDebug(
                    "Ended span {SpanId} in trace {TraceId}, duration {Duration}ms",
                    spanId, traceId, span.Duration?.TotalMilliseconds ?? 0);
            }
        }

        /// <summary>
        /// End a trace
        /// </summary>
        public void EndTrace(string traceId, ExecutionStatus status, string? error = null)
        {
            if (!_activeTraces.TryGetValue(traceId, out var trace))
            {
                _logger.LogWarning("Attempted to end non-existent trace {TraceId}", traceId);
                return;
            }

            trace.EndTime = DateTime.UtcNow;
            trace.Status = status;
            trace.Error = error;

            _logger.LogInformation(
                "Ended trace {TraceId} with status {Status}, duration {Duration}ms",
                traceId, status, trace.Duration?.TotalMilliseconds ?? 0);

            LogAudit($"TRACE_END|{traceId}|{status}|{trace.Duration?.TotalMilliseconds ?? 0}ms");

            // Archive completed trace
            _activeTraces.TryRemove(traceId, out _);
        }

        /// <summary>
        /// Get trace by ID
        /// </summary>
        public ExecutionTrace? GetTrace(string traceId)
        {
            _activeTraces.TryGetValue(traceId, out var trace);
            return trace;
        }

        /// <summary>
        /// Add log entry to trace
        /// </summary>
        public void LogToTrace(string traceId, string message)
        {
            if (_activeTraces.TryGetValue(traceId, out var trace))
            {
                trace.Logs.Add($"{DateTime.UtcNow:HH:mm:ss.fff} {message}");
            }
        }

        #endregion

        #region Metrics

        /// <summary>
        /// Record a metric value
        /// </summary>
        public void RecordMetric(string name, double value, MetricType type = MetricType.Gauge, Dictionary<string, string>? labels = null)
        {
            var dataPoint = new MetricDataPoint
            {
                Name = name,
                Value = value,
                Type = type,
                Labels = labels ?? new()
            };

            _metrics.AddOrUpdate(
                name,
                new List<MetricDataPoint> { dataPoint },
                (key, existing) =>
                {
                    existing.Add(dataPoint);
                    // Keep only recent data points (last 1000)
                    if (existing.Count > 1000)
                    {
                        existing = existing.Skip(existing.Count - 1000).ToList();
                    }
                    return existing;
                });

            _logger.LogTrace("Recorded metric {MetricName} = {Value}", name, value);
        }

        /// <summary>
        /// Increment a counter metric
        /// </summary>
        public void IncrementCounter(string name, double increment = 1.0, Dictionary<string, string>? labels = null)
        {
            RecordMetric(name, increment, MetricType.Counter, labels);
        }

        /// <summary>
        /// Record a histogram value (for latencies, sizes, etc)
        /// </summary>
        public void RecordHistogram(string name, double value, Dictionary<string, string>? labels = null)
        {
            RecordMetric(name, value, MetricType.Histogram, labels);
        }

        /// <summary>
        /// Get metric summary
        /// </summary>
        public MetricsSummary? GetMetricSummary(string name, TimeSpan? timeWindow = null)
        {
            if (!_metrics.TryGetValue(name, out var dataPoints) || !dataPoints.Any())
                return null;

            var startTime = timeWindow.HasValue
                ? DateTime.UtcNow - timeWindow.Value
                : dataPoints.First().Timestamp;

            var relevantPoints = dataPoints
                .Where(dp => dp.Timestamp >= startTime)
                .ToList();

            if (!relevantPoints.Any())
                return null;

            return new MetricsSummary
            {
                MetricName = name,
                Total = relevantPoints.Sum(dp => dp.Value),
                Average = relevantPoints.Average(dp => dp.Value),
                Min = relevantPoints.Min(dp => dp.Value),
                Max = relevantPoints.Max(dp => dp.Value),
                Count = relevantPoints.Count,
                StartTime = relevantPoints.First().Timestamp,
                EndTime = relevantPoints.Last().Timestamp
            };
        }

        /// <summary>
        /// Get all metric names
        /// </summary>
        public List<string> GetMetricNames()
        {
            return _metrics.Keys.ToList();
        }

        /// <summary>
        /// Export metrics in Prometheus format
        /// </summary>
        public string ExportPrometheusMetrics()
        {
            var sb = new StringBuilder();

            foreach (var metricName in _metrics.Keys)
            {
                if (_metrics.TryGetValue(metricName, out var dataPoints))
                {
                    var latest = dataPoints.LastOrDefault();
                    if (latest != null)
                    {
                        // Format: metric_name{label1="value1"} value timestamp
                        var labels = latest.Labels.Any()
                            ? "{" + string.Join(",", latest.Labels.Select(kv => $"{kv.Key}=\"{kv.Value}\"")) + "}"
                            : "";

                        sb.AppendLine($"# TYPE {metricName} {latest.Type.ToString().ToLower()}");
                        sb.AppendLine($"{metricName}{labels} {latest.Value} {new DateTimeOffset(latest.Timestamp).ToUnixTimeMilliseconds()}");
                    }
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Logging

        /// <summary>
        /// Log with structured context
        /// </summary>
        public void LogStructured(LogLevel level, string message, Dictionary<string, object>? context = null)
        {
            var logMessage = message;
            if (context != null && context.Any())
            {
                var contextStr = string.Join(", ", context.Select(kv => $"{kv.Key}={kv.Value}"));
                logMessage = $"{message} | Context: {contextStr}";
            }

            _logger.Log(level, logMessage);

            LogAudit($"LOG|{level}|{message}");
        }

        /// <summary>
        /// Log agent action
        /// </summary>
        public void LogAgentAction(string agentId, string action, Dictionary<string, object>? parameters = null)
        {
            var context = new Dictionary<string, object>
            {
                ["AgentId"] = agentId,
                ["Action"] = action
            };

            if (parameters != null)
            {
                foreach (var kv in parameters)
                {
                    context[kv.Key] = kv.Value;
                }
            }

            LogStructured(LogLevel.Information, $"Agent action: {action}", context);
            LogAudit($"AGENT_ACTION|{agentId}|{action}");
        }

        /// <summary>
        /// Log error with stack trace
        /// </summary>
        public void LogError(string message, Exception? exception = null, Dictionary<string, object>? context = null)
        {
            var fullContext = context ?? new Dictionary<string, object>();
            if (exception != null)
            {
                fullContext["Exception"] = exception.GetType().Name;
                fullContext["StackTrace"] = exception.StackTrace ?? "N/A";
            }

            LogStructured(LogLevel.Error, message, fullContext);

            if (exception != null)
            {
                _logger.LogError(exception, message);
            }
        }

        #endregion

        #region Audit Log

        /// <summary>
        /// Add entry to audit log
        /// </summary>
        private void LogAudit(string entry)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            _auditLog.Enqueue($"{timestamp}|{entry}");

            // Keep audit log size manageable
            while (_auditLog.Count > MAX_AUDIT_LOG_SIZE)
            {
                _auditLog.TryDequeue(out _);
            }
        }

        /// <summary>
        /// Export audit log
        /// </summary>
        public List<string> ExportAuditLog(int? maxEntries = null)
        {
            var entries = _auditLog.ToList();
            if (maxEntries.HasValue && maxEntries.Value < entries.Count)
            {
                entries = entries.Skip(entries.Count - maxEntries.Value).ToList();
            }
            return entries;
        }

        /// <summary>
        /// Search audit log
        /// </summary>
        public List<string> SearchAuditLog(string searchTerm)
        {
            return _auditLog
                .Where(entry => entry.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        #endregion

        #region Health & Statistics

        /// <summary>
        /// Get observability statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["ActiveTraces"] = _activeTraces.Count,
                ["TotalMetrics"] = _metrics.Count,
                ["AuditLogSize"] = _auditLog.Count,
                ["MetricNames"] = GetMetricNames()
            };
        }

        /// <summary>
        /// Clear old data
        /// </summary>
        public void Cleanup(TimeSpan? olderThan = null)
        {
            var threshold = DateTime.UtcNow - (olderThan ?? TimeSpan.FromHours(24));

            // Clean up old metric data
            foreach (var metricName in _metrics.Keys)
            {
                if (_metrics.TryGetValue(metricName, out var dataPoints))
                {
                    var filtered = dataPoints.Where(dp => dp.Timestamp >= threshold).ToList();
                    _metrics[metricName] = filtered;
                }
            }

            _logger.LogInformation("Cleaned up observability data older than {Threshold}", threshold);
        }

        #endregion
    }
}
