namespace AI_AI_Agent.API.Options;

public class UrlSafetyOptions
{
    public const string SectionName = "UrlSafety";

    // Simple substring-based allow/deny patterns for now
    public string[] AllowList { get; set; } = new string[0];
    public string[] DenyList { get; set; } = new string[0];
}
