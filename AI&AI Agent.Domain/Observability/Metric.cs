namespace AI_AI_Agent.Domain.Observability
{
    /// <summary>
    /// Represents a metric data point
    /// </summary>
    public class MetricDataPoint
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Labels { get; set; } = new();
        public MetricType Type { get; set; }
    }

    public enum MetricType
    {
        Counter,
        Gauge,
        Histogram,
        Summary
    }

    /// <summary>
    /// Aggregated metrics for reporting
    /// </summary>
    public class MetricsSummary
    {
        public string MetricName { get; set; } = string.Empty;
        public double Total { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public int Count { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, string> Labels { get; set; } = new();
    }
}
