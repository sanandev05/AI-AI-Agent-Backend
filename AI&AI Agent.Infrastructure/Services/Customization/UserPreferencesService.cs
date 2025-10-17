using AI_AI_Agent.Domain.Customization;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Customization
{
    /// <summary>
    /// User preferences service for per-user settings
    /// </summary>
    public class UserPreferencesService
    {
        private readonly ILogger<UserPreferencesService> _logger;
        private readonly ConcurrentDictionary<string, UserPreferences> _preferences = new();

        public UserPreferencesService(ILogger<UserPreferencesService> logger)
        {
            _logger = logger;
        }

        #region User Preferences Management

        public UserPreferences GetOrCreatePreferences(string userId)
        {
            if (!_preferences.ContainsKey(userId))
            {
                _preferences[userId] = new UserPreferences
                {
                    UserId = userId,
                    Personality = new AgentPersonality { Name = "Default" },
                    ToolPrefs = new ToolPreferences { UserId = userId },
                    ResponseStyle = new ResponseStyle(),
                    Notifications = new NotificationPreferences()
                };
                _logger.LogInformation("Created default preferences for user {UserId}", userId);
            }
            return _preferences[userId];
        }

        public UserPreferences UpdatePreferences(string userId, UserPreferences updates)
        {
            var prefs = GetOrCreatePreferences(userId);
            prefs.Personality = updates.Personality ?? prefs.Personality;
            prefs.ToolPrefs = updates.ToolPrefs ?? prefs.ToolPrefs;
            prefs.ResponseStyle = updates.ResponseStyle ?? prefs.ResponseStyle;
            prefs.Notifications = updates.Notifications ?? prefs.Notifications;
            prefs.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updated preferences for user {UserId}", userId);
            return prefs;
        }

        #endregion

        #region Favorites

        public void AddFavoriteTool(string userId, string toolName)
        {
            var prefs = GetOrCreatePreferences(userId);
            if (!prefs.FavoriteTools.Contains(toolName))
            {
                prefs.FavoriteTools.Add(toolName);
                _logger.LogInformation("Added favorite tool {Tool} for user {UserId}", toolName, userId);
            }
        }

        public void RemoveFavoriteTool(string userId, string toolName)
        {
            var prefs = GetOrCreatePreferences(userId);
            prefs.FavoriteTools.Remove(toolName);
            _logger.LogInformation("Removed favorite tool {Tool} for user {UserId}", toolName, userId);
        }

        public void AddFavoriteAgent(string userId, string agentType)
        {
            var prefs = GetOrCreatePreferences(userId);
            if (!prefs.FavoriteAgents.Contains(agentType))
            {
                prefs.FavoriteAgents.Add(agentType);
                _logger.LogInformation("Added favorite agent {Agent} for user {UserId}", agentType, userId);
            }
        }

        public void RemoveFavoriteAgent(string userId, string agentType)
        {
            var prefs = GetOrCreatePreferences(userId);
            prefs.FavoriteAgents.Remove(agentType);
            _logger.LogInformation("Removed favorite agent {Agent} for user {UserId}", agentType, userId);
        }

        #endregion

        #region Custom Shortcuts

        public void SetShortcut(string userId, string shortcut, string command)
        {
            var prefs = GetOrCreatePreferences(userId);
            prefs.CustomShortcuts[shortcut] = command;
            _logger.LogInformation("Set shortcut {Shortcut} for user {UserId}", shortcut, userId);
        }

        public string? GetShortcutCommand(string userId, string shortcut)
        {
            var prefs = GetOrCreatePreferences(userId);
            return prefs.CustomShortcuts.TryGetValue(shortcut, out var command) ? command : null;
        }

        public Dictionary<string, string> GetAllShortcuts(string userId)
        {
            var prefs = GetOrCreatePreferences(userId);
            return new Dictionary<string, string>(prefs.CustomShortcuts);
        }

        #endregion

        #region Notification Preferences

        public void UpdateNotificationPreferences(string userId, NotificationPreferences notifications)
        {
            var prefs = GetOrCreatePreferences(userId);
            prefs.Notifications = notifications;
            _logger.LogInformation("Updated notification preferences for user {UserId}", userId);
        }

        public NotificationPreferences GetNotificationPreferences(string userId)
        {
            var prefs = GetOrCreatePreferences(userId);
            return prefs.Notifications ?? new NotificationPreferences();
        }

        #endregion

        #region Export/Import

        public string ExportPreferences(string userId)
        {
            var prefs = GetOrCreatePreferences(userId);
            var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation("Exported preferences for user {UserId}", userId);
            return json;
        }

        public UserPreferences ImportPreferences(string userId, string json)
        {
            var prefs = JsonSerializer.Deserialize<UserPreferences>(json);
            if (prefs != null)
            {
                prefs.UserId = userId;
                _preferences[userId] = prefs;
                _logger.LogInformation("Imported preferences for user {UserId}", userId);
                return prefs;
            }
            throw new InvalidOperationException("Failed to deserialize preferences");
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalUsers"] = _preferences.Count,
                ["usersWithCustomShortcuts"] = _preferences.Values.Count(p => p.CustomShortcuts.Any()),
                ["usersWithFavoriteTools"] = _preferences.Values.Count(p => p.FavoriteTools.Any()),
                ["usersWithFavoriteAgents"] = _preferences.Values.Count(p => p.FavoriteAgents.Any()),
                ["avgShortcutsPerUser"] = _preferences.Values.Any() ? _preferences.Values.Average(p => p.CustomShortcuts.Count) : 0
            };
        }

        #endregion
    }
}
