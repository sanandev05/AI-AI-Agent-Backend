using AI_AI_Agent.Domain.Streaming;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Chat
{
    /// <summary>
    /// Conversation management service with branching, editing, and export/import
    /// </summary>
    public class ConversationManagementService
    {
        private readonly ILogger<ConversationManagementService> _logger;
        private readonly ConcurrentDictionary<string, List<ConversationBranch>> _branches = new();
        private readonly ConcurrentDictionary<string, ConversationShareLink> _shareLinks = new();

        public ConversationManagementService(ILogger<ConversationManagementService> logger)
        {
            _logger = logger;
        }

        #region Conversation Branching

        /// <summary>
        /// Create a new conversation branch from a message
        /// </summary>
        public ConversationBranch CreateBranch(
            string conversationId,
            string parentMessageId,
            string branchName)
        {
            var branch = new ConversationBranch
            {
                ParentMessageId = parentMessageId,
                Name = branchName
            };

            if (!_branches.ContainsKey(conversationId))
            {
                _branches[conversationId] = new List<ConversationBranch>();
            }

            _branches[conversationId].Add(branch);
            _logger.LogInformation("Created branch {BranchName} for conversation {ConversationId}", 
                branchName, conversationId);

            return branch;
        }

        /// <summary>
        /// Get all branches for a conversation
        /// </summary>
        public List<ConversationBranch> GetBranches(string conversationId)
        {
            return _branches.TryGetValue(conversationId, out var branches) 
                ? branches 
                : new List<ConversationBranch>();
        }

        /// <summary>
        /// Switch to a different branch
        /// </summary>
        public ConversationBranch? SwitchBranch(string conversationId, string branchId)
        {
            var branches = GetBranches(conversationId);
            var targetBranch = branches.FirstOrDefault(b => b.Id == branchId);

            if (targetBranch != null)
            {
                // Deactivate all branches
                foreach (var branch in branches)
                {
                    branch.IsActive = false;
                }

                // Activate target branch
                targetBranch.IsActive = true;
                _logger.LogInformation("Switched to branch {BranchId} in conversation {ConversationId}", 
                    branchId, conversationId);
            }

            return targetBranch;
        }

        #endregion

        #region Message Editing

        /// <summary>
        /// Edit a message and create a new branch
        /// </summary>
        public (string newMessageId, ConversationBranch branch) EditMessage(
            string conversationId,
            string messageId,
            string newContent)
        {
            var newMessageId = Guid.NewGuid().ToString();
            var branch = CreateBranch(conversationId, messageId, $"Edit at {DateTime.UtcNow:HH:mm:ss}");
            branch.MessageIds.Add(newMessageId);

            _logger.LogInformation("Edited message {MessageId} in conversation {ConversationId}", 
                messageId, conversationId);

            return (newMessageId, branch);
        }

        /// <summary>
        /// Regenerate a message
        /// </summary>
        public string RegenerateMessage(string conversationId, string messageId)
        {
            var newMessageId = Guid.NewGuid().ToString();
            var branch = CreateBranch(conversationId, messageId, $"Regenerate at {DateTime.UtcNow:HH:mm:ss}");
            branch.MessageIds.Add(newMessageId);

            _logger.LogInformation("Regenerated message {MessageId} in conversation {ConversationId}", 
                messageId, conversationId);

            return newMessageId;
        }

        #endregion

        #region Export/Import

        /// <summary>
        /// Export conversation to JSON
        /// </summary>
        public ConversationExport ExportConversation(
            string conversationId,
            string title,
            List<ExportedMessage> messages)
        {
            var export = new ConversationExport
            {
                ConversationId = conversationId,
                Title = title,
                Messages = messages,
                Metadata = new Dictionary<string, object>
                {
                    ["branches"] = GetBranches(conversationId).Count,
                    ["exportVersion"] = "1.0"
                }
            };

            _logger.LogInformation("Exported conversation {ConversationId} with {MessageCount} messages", 
                conversationId, messages.Count);

            return export;
        }

        /// <summary>
        /// Export conversation to JSON string
        /// </summary>
        public string ExportToJson(ConversationExport export)
        {
            return JsonSerializer.Serialize(export, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
        }

        /// <summary>
        /// Export conversation to Markdown
        /// </summary>
        public string ExportToMarkdown(ConversationExport export)
        {
            var markdown = new System.Text.StringBuilder();
            markdown.AppendLine($"# {export.Title}");
            markdown.AppendLine($"\n*Exported: {export.ExportedAt:yyyy-MM-dd HH:mm:ss}*\n");

            foreach (var message in export.Messages)
            {
                markdown.AppendLine($"## {message.Role}");
                markdown.AppendLine($"*{message.Timestamp:yyyy-MM-dd HH:mm:ss}*\n");
                markdown.AppendLine(message.Content);
                markdown.AppendLine();
            }

            _logger.LogInformation("Exported conversation {ConversationId} to Markdown", 
                export.ConversationId);

            return markdown.ToString();
        }

        /// <summary>
        /// Import conversation from JSON
        /// </summary>
        public ConversationExport ImportFromJson(string json)
        {
            var export = JsonSerializer.Deserialize<ConversationExport>(json);
            if (export == null)
            {
                throw new InvalidOperationException("Failed to deserialize conversation export");
            }

            _logger.LogInformation("Imported conversation with {MessageCount} messages", 
                export.Messages.Count);

            return export;
        }

        #endregion

        #region Sharing

        /// <summary>
        /// Create a share link for conversation
        /// </summary>
        public ConversationShareLink CreateShareLink(
            string conversationId,
            string createdBy,
            TimeSpan? expiresIn = null)
        {
            var shareLink = new ConversationShareLink
            {
                ConversationId = conversationId,
                CreatedBy = createdBy,
                ExpiresAt = expiresIn.HasValue ? DateTime.UtcNow.Add(expiresIn.Value) : null
            };

            _shareLinks[shareLink.ShareToken] = shareLink;

            _logger.LogInformation("Created share link {Token} for conversation {ConversationId}", 
                shareLink.ShareToken, conversationId);

            return shareLink;
        }

        /// <summary>
        /// Get conversation by share token
        /// </summary>
        public string? GetConversationByShareToken(string shareToken)
        {
            if (_shareLinks.TryGetValue(shareToken, out var shareLink))
            {
                // Check expiration
                if (shareLink.ExpiresAt.HasValue && shareLink.ExpiresAt.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Share link {Token} has expired", shareToken);
                    return null;
                }

                shareLink.AccessCount++;
                shareLink.LastAccessedAt = DateTime.UtcNow;

                return shareLink.ConversationId;
            }

            return null;
        }

        /// <summary>
        /// Revoke share link
        /// </summary>
        public bool RevokeShareLink(string shareToken)
        {
            if (_shareLinks.TryRemove(shareToken, out _))
            {
                _logger.LogInformation("Revoked share link {Token}", shareToken);
                return true;
            }

            return false;
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get conversation statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalConversations"] = _branches.Count,
                ["totalBranches"] = _branches.Values.Sum(b => b.Count),
                ["activeShareLinks"] = _shareLinks.Count,
                ["totalShares"] = _shareLinks.Values.Sum(s => s.AccessCount)
            };
        }

        #endregion
    }

    /// <summary>
    /// Share link for conversation
    /// </summary>
    public class ConversationShareLink
    {
        public string ShareToken { get; set; } = Guid.NewGuid().ToString("N");
        public string ConversationId { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public int AccessCount { get; set; } = 0;
        public DateTime? LastAccessedAt { get; set; }
    }
}
