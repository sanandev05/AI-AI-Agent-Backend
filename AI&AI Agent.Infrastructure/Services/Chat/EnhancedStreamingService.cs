using AI_AI_Agent.Domain.Streaming;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;

namespace AI_AI_Agent.Infrastructure.Services.Chat
{
    /// <summary>
    /// Enhanced streaming service for real-time chat updates
    /// </summary>
    public class EnhancedStreamingService
    {
        private readonly ILogger<EnhancedStreamingService> _logger;
        private readonly ConcurrentDictionary<string, StreamingSession> _activeSessions = new();

        public EnhancedStreamingService(ILogger<EnhancedStreamingService> logger)
        {
            _logger = logger;
        }

        #region Session Management

        /// <summary>
        /// Create a new streaming session
        /// </summary>
        public StreamingSession CreateSession(string userId, string conversationId)
        {
            var session = new StreamingSession
            {
                UserId = userId,
                ConversationId = conversationId
            };

            _activeSessions[session.Id] = session;
            _logger.LogInformation("Created streaming session {SessionId} for user {UserId}", 
                session.Id, userId);

            return session;
        }

        /// <summary>
        /// Get active session
        /// </summary>
        public StreamingSession? GetSession(string sessionId)
        {
            _activeSessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Close streaming session
        /// </summary>
        public void CloseSession(string sessionId)
        {
            if (_activeSessions.TryRemove(sessionId, out var session))
            {
                session.IsActive = false;
                _logger.LogInformation("Closed streaming session {SessionId}", sessionId);
            }
        }

        #endregion

        #region Streaming

        /// <summary>
        /// Stream a text chunk
        /// </summary>
        public async Task StreamTextAsync(
            string sessionId,
            string messageId,
            string content,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = messageId,
                Type = StreamChunkType.Text,
                Content = content
            };

            session.Chunks.Add(chunk);
            session.OnChunkReceived?.Invoke(chunk);

            _logger.LogDebug("Streamed text chunk to session {SessionId}", sessionId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Show typing indicator
        /// </summary>
        public async Task ShowTypingIndicatorAsync(
            string sessionId,
            string agentName,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = StreamChunkType.TypingIndicator,
                Content = $"{agentName} is typing...",
                Metadata = new Dictionary<string, object>
                {
                    ["agentName"] = agentName
                }
            };

            session.OnChunkReceived?.Invoke(chunk);
            _logger.LogDebug("Showed typing indicator for {Agent}", agentName);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stream tool execution progress
        /// </summary>
        public async Task StreamToolExecutionAsync(
            string sessionId,
            ToolExecutionProgress progress,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = StreamChunkType.ToolExecution,
                Content = JsonSerializer.Serialize(progress),
                Metadata = new Dictionary<string, object>
                {
                    ["toolName"] = progress.ToolName,
                    ["status"] = progress.Status,
                    ["progress"] = progress.Progress
                }
            };

            session.Chunks.Add(chunk);
            session.OnChunkReceived?.Invoke(chunk);

            _logger.LogDebug("Streamed tool execution {Tool} with status {Status}", 
                progress.ToolName, progress.Status);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stream reasoning trace
        /// </summary>
        public async Task StreamReasoningTraceAsync(
            string sessionId,
            ReasoningTraceDisplay trace,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = StreamChunkType.ReasoningTrace,
                Content = JsonSerializer.Serialize(trace),
                Metadata = new Dictionary<string, object>
                {
                    ["steps"] = trace.Steps.Count,
                    ["confidence"] = trace.Confidence
                }
            };

            session.Chunks.Add(chunk);
            session.OnChunkReceived?.Invoke(chunk);

            _logger.LogDebug("Streamed reasoning trace with {Steps} steps", trace.Steps.Count);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stream progress update
        /// </summary>
        public async Task StreamProgressUpdateAsync(
            string sessionId,
            string message,
            double progress,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = StreamChunkType.ProgressUpdate,
                Content = message,
                Metadata = new Dictionary<string, object>
                {
                    ["progress"] = progress
                }
            };

            session.OnChunkReceived?.Invoke(chunk);
            _logger.LogDebug("Streamed progress update: {Progress:P0}", progress);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Mark streaming as complete
        /// </summary>
        public async Task CompleteStreamingAsync(
            string sessionId,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = messageId,
                Type = StreamChunkType.Complete,
                Content = "Streaming complete"
            };

            session.Chunks.Add(chunk);
            session.OnChunkReceived?.Invoke(chunk);

            _logger.LogInformation("Completed streaming for session {SessionId}", sessionId);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Stream error
        /// </summary>
        public async Task StreamErrorAsync(
            string sessionId,
            string error,
            CancellationToken cancellationToken = default)
        {
            var session = GetSession(sessionId);
            if (session == null) return;

            var chunk = new StreamChunk
            {
                MessageId = Guid.NewGuid().ToString(),
                Type = StreamChunkType.Error,
                Content = error
            };

            session.Chunks.Add(chunk);
            session.OnChunkReceived?.Invoke(chunk);

            _logger.LogError("Streamed error: {Error}", error);
            await Task.CompletedTask;
        }

        #endregion

        #region Visualization

        /// <summary>
        /// Get tool execution visualization data
        /// </summary>
        public List<ToolExecutionProgress> GetToolExecutions(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session == null) return new List<ToolExecutionProgress>();

            return session.Chunks
                .Where(c => c.Type == StreamChunkType.ToolExecution)
                .Select(c => JsonSerializer.Deserialize<ToolExecutionProgress>(c.Content))
                .Where(p => p != null)
                .Cast<ToolExecutionProgress>()
                .ToList();
        }

        /// <summary>
        /// Get reasoning traces
        /// </summary>
        public List<ReasoningTraceDisplay> GetReasoningTraces(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session == null) return new List<ReasoningTraceDisplay>();

            return session.Chunks
                .Where(c => c.Type == StreamChunkType.ReasoningTrace)
                .Select(c => JsonSerializer.Deserialize<ReasoningTraceDisplay>(c.Content))
                .Where(t => t != null)
                .Cast<ReasoningTraceDisplay>()
                .ToList();
        }

        /// <summary>
        /// Get session statistics
        /// </summary>
        public Dictionary<string, object> GetSessionStatistics(string sessionId)
        {
            var session = GetSession(sessionId);
            if (session == null) return new Dictionary<string, object>();

            return new Dictionary<string, object>
            {
                ["sessionId"] = sessionId,
                ["userId"] = session.UserId,
                ["conversationId"] = session.ConversationId,
                ["isActive"] = session.IsActive,
                ["totalChunks"] = session.Chunks.Count,
                ["textChunks"] = session.Chunks.Count(c => c.Type == StreamChunkType.Text),
                ["toolExecutions"] = session.Chunks.Count(c => c.Type == StreamChunkType.ToolExecution),
                ["reasoningTraces"] = session.Chunks.Count(c => c.Type == StreamChunkType.ReasoningTrace),
                ["startedAt"] = session.StartedAt
            };
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get global streaming statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["activeSessions"] = _activeSessions.Count,
                ["totalChunksStreamed"] = _activeSessions.Values.Sum(s => s.Chunks.Count),
                ["activeUsers"] = _activeSessions.Values.Select(s => s.UserId).Distinct().Count()
            };
        }

        #endregion
    }

    /// <summary>
    /// Streaming session
    /// </summary>
    public class StreamingSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string ConversationId { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public List<StreamChunk> Chunks { get; set; } = new();
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public Action<StreamChunk>? OnChunkReceived { get; set; }
    }
}
