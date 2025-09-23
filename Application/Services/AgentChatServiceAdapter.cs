using AI_AI_Agent.Contract.DTOs;
using AI_AI_Agent.Contract.Services;
using AI_AI_Agent.Domain.Agents;

namespace AI_AI_Agent.Application.Services
{
    // NOTE: Register this instead of the old implementation once ready.
    public class AgentChatServiceAdapter : IChatService
    {
        private readonly IAgent _agent;

        public AgentChatServiceAdapter(IAgent agent)
        {
            _agent = agent;
        }

        public IAsyncEnumerable<string> StreamChatAsync(ChatRequestDto request, string userId)
        {
            // TODO: Map request to userMessage (e.g., request.Message)
            var userMessage = GetUserMessage(request);
            return _agent.StreamAsync(userId, request.ChatId!, userMessage);
        }

        public IAsyncEnumerable<string> StreamWebSearchAsync(WebSearchRequestDto request, string userId, CancellationToken ct)
        {
            // TODO: Keep existing web search logic or integrate as future plugin.
            return EmptyAsync(); // placeholder
        }

        // --- The remaining members of IChatService must be implemented; use existing logic or delegate. ---
        // TODO: Implement: CreateChatAsync, GetChatsByUserIdAsync, GetChatByUIdAsync, DeleteChatAsync, RenameChatAsync, etc.

        private static string GetUserMessage(ChatRequestDto request)
        {
            // TODO: adapt to actual property name
            var prop = request.GetType().GetProperty("Message");
            return prop?.GetValue(request)?.ToString() ?? string.Empty;
        }

        private static async IAsyncEnumerable<string> EmptyAsync()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
