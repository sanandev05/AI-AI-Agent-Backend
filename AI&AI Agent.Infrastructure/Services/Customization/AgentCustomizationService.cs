using AI_AI_Agent.Domain.Customization;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Customization
{
    /// <summary>
    /// Agent customization service for personality, templates, and preferences
    /// </summary>
    public class AgentCustomizationService
    {
        private readonly ILogger<AgentCustomizationService> _logger;
        private readonly ConcurrentDictionary<string, AgentPersonality> _personalities = new();
        private readonly ConcurrentDictionary<string, InstructionTemplate> _templates = new();
        private readonly ConcurrentDictionary<string, ToolPreferences> _toolPreferences = new();
        private readonly ConcurrentDictionary<string, ResponseStyle> _responseStyles = new();

        public AgentCustomizationService(ILogger<AgentCustomizationService> logger)
        {
            _logger = logger;
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            // Default personalities
            _personalities["professional"] = new AgentPersonality
            {
                Name = "Professional",
                Tone = "professional",
                Verbosity = "balanced",
                UseEmojis = false,
                UseTechnicalJargon = true
            };

            _personalities["friendly"] = new AgentPersonality
            {
                Name = "Friendly",
                Tone = "friendly",
                Verbosity = "detailed",
                UseEmojis = true,
                UseTechnicalJargon = false
            };

            // Default templates
            _templates["code-assistant"] = new InstructionTemplate
            {
                Name = "Code Assistant",
                Description = "Specialized in code generation and debugging",
                SystemPrompt = "You are an expert software engineer. Provide clear, well-documented code solutions.",
                IsDefault = false
            };

            _templates["research-assistant"] = new InstructionTemplate
            {
                Name = "Research Assistant",
                Description = "Specialized in research and information gathering",
                SystemPrompt = "You are a thorough researcher. Provide comprehensive, well-cited information.",
                IsDefault = false
            };
        }

        #region Personality Customization

        public AgentPersonality CreatePersonality(AgentPersonality personality)
        {
            _personalities[personality.Id] = personality;
            _logger.LogInformation("Created personality {Name}", personality.Name);
            return personality;
        }

        public AgentPersonality? GetPersonality(string personalityId)
        {
            return _personalities.TryGetValue(personalityId, out var personality) ? personality : null;
        }

        public List<AgentPersonality> GetAllPersonalities()
        {
            return _personalities.Values.ToList();
        }

        public AgentPersonality UpdatePersonality(string personalityId, AgentPersonality updates)
        {
            if (_personalities.TryGetValue(personalityId, out var personality))
            {
                personality.Tone = updates.Tone;
                personality.Verbosity = updates.Verbosity;
                personality.UseEmojis = updates.UseEmojis;
                personality.UseTechnicalJargon = updates.UseTechnicalJargon;
                personality.CustomTraits = updates.CustomTraits;
                _logger.LogInformation("Updated personality {Id}", personalityId);
            }
            return personality!;
        }

        #endregion

        #region Instruction Templates

        public InstructionTemplate CreateTemplate(InstructionTemplate template)
        {
            _templates[template.Id] = template;
            _logger.LogInformation("Created instruction template {Name}", template.Name);
            return template;
        }

        public InstructionTemplate? GetTemplate(string templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        public List<InstructionTemplate> GetAllTemplates()
        {
            return _templates.Values.ToList();
        }

        public string ApplyTemplate(InstructionTemplate template, Dictionary<string, string> variables)
        {
            var prompt = template.SystemPrompt;
            foreach (var variable in variables)
            {
                prompt = prompt.Replace($"{{{variable.Key}}}", variable.Value);
            }
            _logger.LogDebug("Applied template {TemplateName} with {Count} variables", template.Name, variables.Count);
            return prompt;
        }

        #endregion

        #region Tool Preferences

        public ToolPreferences SetToolPreferences(string userId, ToolPreferences preferences)
        {
            preferences.UserId = userId;
            _toolPreferences[userId] = preferences;
            _logger.LogInformation("Set tool preferences for user {UserId}", userId);
            return preferences;
        }

        public ToolPreferences? GetToolPreferences(string userId)
        {
            return _toolPreferences.TryGetValue(userId, out var prefs) ? prefs : null;
        }

        public bool IsToolAllowed(string userId, string toolName)
        {
            var prefs = GetToolPreferences(userId);
            if (prefs == null) return true;

            if (prefs.DisabledTools.Contains(toolName)) return false;
            return true;
        }

        #endregion

        #region Response Style

        public ResponseStyle SetResponseStyle(string userId, ResponseStyle style)
        {
            _responseStyles[userId] = style;
            _logger.LogInformation("Set response style for user {UserId}", userId);
            return style;
        }

        public ResponseStyle? GetResponseStyle(string userId)
        {
            return _responseStyles.TryGetValue(userId, out var style) ? style : null;
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalPersonalities"] = _personalities.Count,
                ["totalTemplates"] = _templates.Count,
                ["usersWithToolPreferences"] = _toolPreferences.Count,
                ["usersWithResponseStyles"] = _responseStyles.Count
            };
        }

        #endregion
    }
}
