namespace AI_AI_Agent.Domain.CodeInterpreter
{
    /// <summary>
    /// Code execution environment with enhanced capabilities
    /// </summary>
    public class CodeExecution
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public ExecutionMode Mode { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public ExecutionStatus Status { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public List<ExecutionArtifact> Artifacts { get; set; } = new();
        public ExecutionEnvironment Environment { get; set; } = new();
        public List<string> InstalledPackages { get; set; } = new();
    }

    public enum ExecutionMode
    {
        Standard,
        Interactive,
        Debug,
        DataAnalysis
    }

    public enum ExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    /// <summary>
    /// Execution environment configuration
    /// </summary>
    public class ExecutionEnvironment
    {
        public string RuntimeVersion { get; set; } = string.Empty;
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public List<string> AllowedModules { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 300;
        public long MemoryLimitMB { get; set; } = 512;
        public string WorkingDirectory { get; set; } = string.Empty;
    }

    /// <summary>
    /// Artifacts produced during execution
    /// </summary>
    public class ExecutionArtifact
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public ArtifactType Type { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? Content { get; set; }
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum ArtifactType
    {
        Plot,
        Chart,
        DataFile,
        Image,
        Text,
        Binary
    }

    /// <summary>
    /// Data analysis results
    /// </summary>
    public class DataAnalysisResult
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DatasetName { get; set; } = string.Empty;
        public DataStatistics Statistics { get; set; } = new();
        public List<Visualization> Visualizations { get; set; } = new();
        public List<Insight> Insights { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    public class DataStatistics
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<ColumnInfo> Columns { get; set; } = new();
        public Dictionary<string, object> SummaryStats { get; set; } = new();
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public int NullCount { get; set; }
        public object? MinValue { get; set; }
        public object? MaxValue { get; set; }
        public object? MeanValue { get; set; }
    }

    public class Visualization
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public VisualizationType Type { get; set; }
        public string ImagePath { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public enum VisualizationType
    {
        LineChart,
        BarChart,
        ScatterPlot,
        Histogram,
        HeatMap,
        BoxPlot,
        PieChart
    }

    public class Insight
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public InsightType Type { get; set; }
        public double Confidence { get; set; }
    }

    public enum InsightType
    {
        Trend,
        Correlation,
        Anomaly,
        Pattern,
        Outlier
    }

    /// <summary>
    /// Interactive debugging session
    /// </summary>
    public class DebugSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ExecutionId { get; set; } = string.Empty;
        public List<Breakpoint> Breakpoints { get; set; } = new();
        public List<Variable> Variables { get; set; } = new();
        public string? CurrentLine { get; set; }
        public List<string> CallStack { get; set; } = new();
        public DebugState State { get; set; }
    }

    public class Breakpoint
    {
        public int LineNumber { get; set; }
        public string? Condition { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public class Variable
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }

    public enum DebugState
    {
        NotStarted,
        Running,
        Paused,
        StepOver,
        StepInto,
        StepOut,
        Completed
    }

    /// <summary>
    /// Package management
    /// </summary>
    public class PackageInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new();
        public bool IsInstalled { get; set; }
        public bool IsSafe { get; set; } = true;
        public string? SecurityIssue { get; set; }
    }
}
