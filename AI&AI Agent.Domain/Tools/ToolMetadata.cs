namespace AI_AI_Agent.Domain.Tools
{
    /// <summary>
    /// Metadata about a tool's capabilities and requirements
    /// </summary>
    public class ToolMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Categories { get; set; } = new();
        public List<string> RequiredCapabilities { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public double AverageDuration { get; set; } = 0;
        public int Priority { get; set; } = 5; // 1-10, higher = more preferred
        public bool RequiresApproval { get; set; } = false;
        public bool IsExpensive { get; set; } = false;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Tool selection criteria for context-aware routing
    /// </summary>
    public class ToolSelectionCriteria
    {
        public string TaskDescription { get; set; } = string.Empty;
        public List<string> RequiredCategories { get; set; } = new();
        public List<string> PreferredTools { get; set; } = new();
        public List<string> ExcludedTools { get; set; } = new();
        public string AgentType { get; set; } = string.Empty;
        public int MaxTools { get; set; } = 5;
        public bool AllowExpensiveTools { get; set; } = true;
        public Dictionary<string, object> Context { get; set; } = new();
    }

    /// <summary>
    /// Result of tool selection with confidence scoring
    /// </summary>
    public class ToolSelectionResult
    {
        public string ToolName { get; set; } = string.Empty;
        public double ConfidenceScore { get; set; } = 0.0;
        public string Reasoning { get; set; } = string.Empty;
        public ToolMetadata Metadata { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
