namespace AI_AI_Agent.Domain.Reasoning
{
    /// <summary>
    /// Represents a single step in a chain of reasoning
    /// </summary>
    public class ReasoningStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int StepNumber { get; set; }
        public string Thought { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Observation { get; set; } = string.Empty;
        public double Confidence { get; set; } = 1.0;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents a complete reasoning trace
    /// </summary>
    public class ReasoningTrace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Goal { get; set; } = string.Empty;
        public List<ReasoningStep> Steps { get; set; } = new();
        public string Conclusion { get; set; } = string.Empty;
        public bool IsVerified { get; set; } = false;
        public List<string> VerificationChecks { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
