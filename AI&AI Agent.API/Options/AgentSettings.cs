using System.Collections.Generic;

namespace AI_AI_Agent.API.Options;

public class AgentSettings
{
    public const string SectionName = "Agent";

    public string WorkspacePath { get; set; } = "workspace";
    public Dictionary<string, LLMBackendSettings> Backends { get; set; } = new();
}

public class LLMBackendSettings
{
    public string Provider { get; set; } = "AzureOpenAI"; // or "OpenAI", "Gemini"
    public string ModelId { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? OrgId { get; set; } // Optional for OpenAI
}
