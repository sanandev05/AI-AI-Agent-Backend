namespace AI_AI_Agent.Domain.DecisionMaking
{
    /// <summary>
    /// Represents a decision point in agent execution
    /// </summary>
    public class Decision
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Context { get; set; } = string.Empty;
        public List<DecisionOption> Options { get; set; } = new();
        public DecisionOption? SelectedOption { get; set; }
        public DecisionStrategy Strategy { get; set; } = DecisionStrategy.HighestConfidence;
        public bool RequiresApproval { get; set; } = false;
        public bool IsApproved { get; set; } = false;
        public string? ApprovalReason { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DecidedAt { get; set; }
    }

    /// <summary>
    /// Represents a possible choice in a decision
    /// </summary>
    public class DecisionOption
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0.5;
        public Dictionary<string, double> Criteria { get; set; } = new(); // e.g., "cost": 0.8, "speed": 0.6
        public string Reasoning { get; set; } = string.Empty;
        public List<string> Pros { get; set; } = new();
        public List<string> Cons { get; set; } = new();
        public string? FallbackOptionId { get; set; }
    }

    public enum DecisionStrategy
    {
        HighestConfidence,
        WeightedCriteria,
        MinimizeRisk,
        MaximizeReward,
        Balanced,
        UserChoice
    }
}
