using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AI_AI_Agent.Domain.Agents
{
    public class GeneralAgent : IAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chat;

        public GeneralAgent(Kernel kernel, IChatCompletionService chat)
        {
            _kernel = kernel;
            _chat = chat;
        }

        public async IAsyncEnumerable<string> StreamAsync(string userId, string chatId, string userMessage, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // TODO: Load persisted chat history for (userId, chatId) and append to chatHistory before user message.
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(BuildSystemPrompt());
            chatHistory.AddUserMessage(userMessage); // TODO: sanitize / apply policy

            var settings = new ChatCompletionExecutionSettings
            {
                // TODO: expose to request DTO if needed
                Temperature = 0.7,
                TopP = 0.95,
            };

            await foreach (var content in _chat.GetStreamingChatMessageContentsAsync(chatHistory, settings, _kernel, ct))
            {
                if (content.Content is { Length: > 0 })
                    yield return content.Content;
            }

            // TODO: Persist assistant response (aggregate chunks) to chat history store.
        }

        private string BuildSystemPrompt()
        {
            // Extend later (tools, retrieval context, policies)
            return "You are an AI assistant. Be concise, helpful, and truthful.";
        }
    }
}
