namespace AI_AI_Agent.Domain.Customization
{
    /// <summary>
    /// Agent personality configuration
    /// </summary>
    public class AgentPersonality
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Tone { get; set; } = "professional"; // professional, friendly, casual, technical
        public string Verbosity { get; set; } = "balanced"; // concise, balanced, detailed
        public bool UseEmojis { get; set; } = false;
        public bool UseTechnicalJargon { get; set; } = true;
        public Dictionary<string, string> CustomTraits { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Instruction template for agent behavior
    /// </summary>
    public class InstructionTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SystemPrompt { get; set; } = string.Empty;
        public Dictionary<string, string> Variables { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Tool selection preferences
    /// </summary>
    public class ToolPreferences
    {
        public string UserId { get; set; } = string.Empty;
        public List<string> PreferredTools { get; set; } = new();
        public List<string> DisabledTools { get; set; } = new();
        public Dictionary<string, int> ToolPriorities { get; set; } = new(); // toolName -> priority
        public bool AllowExpensiveTools { get; set; } = false;
        public bool RequireApprovalForTools { get; set; } = true;
    }

    /// <summary>
    /// Response style configuration
    /// </summary>
    public class ResponseStyle
    {
        public string Format { get; set; } = "markdown"; // markdown, plain, html
        public bool IncludeReasoningTrace { get; set; } = false;
        public bool ShowToolExecutions { get; set; } = true;
        public bool ShowConfidenceScores { get; set; } = false;
        public bool StreamingEnabled { get; set; } = true;
        public int MaxResponseLength { get; set; } = 4000;
    }

    /// <summary>
    /// User preferences
    /// </summary>
    public class UserPreferences
    {
        public string UserId { get; set; } = string.Empty;
        public AgentPersonality? Personality { get; set; }
        public ToolPreferences? ToolPrefs { get; set; }
        public ResponseStyle? ResponseStyle { get; set; }
        public List<string> FavoriteTools { get; set; } = new();
        public List<string> FavoriteAgents { get; set; } = new();
        public Dictionary<string, string> CustomShortcuts { get; set; } = new();
        public NotificationPreferences? Notifications { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Notification preferences
    /// </summary>
    public class NotificationPreferences
    {
        public bool EmailNotifications { get; set; } = false;
        public bool ToolExecutionNotifications { get; set; } = true;
        public bool ErrorNotifications { get; set; } = true;
        public bool CompletionNotifications { get; set; } = false;
        public List<string> NotificationChannels { get; set; } = new(); // email, sms, push
    }
}
