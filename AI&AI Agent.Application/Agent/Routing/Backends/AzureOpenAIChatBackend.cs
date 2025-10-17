using AI_AI_Agent.Contracts;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Contracts = AI_AI_Agent.Contracts;

namespace AI_AI_Agent.Application.Agent.Routing.Backends;

public class AzureOpenAIChatBackend : IChatBackend
{
    private readonly IChatCompletionService _chatCompletionService;
    public string Name { get; }

    public AzureOpenAIChatBackend(string modelId, Kernel kernel)
    {
        Name = modelId;
        _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>(modelId);
    }

    public async Task<IChatResult> CompleteAsync(
        string system,
        string user,
        IEnumerable<Contracts.ChatMessageContent> history,
        object? toolSchema,
        CancellationToken ct = default)
    {
        var chatHistory = new ChatHistory(system);
        foreach (var message in history)
        {
            if (message.Role.Equals("tool", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
            else
            {
                chatHistory.AddMessage(new AuthorRole(message.Role), message.Content);
            }
        }
        chatHistory.AddUserMessage(user);

        var settings = new PromptExecutionSettings();
        if (toolSchema is not null)
        {
            // Assuming toolSchema is already in the correct format for the provider
            // This part might need adjustment based on how tool schemas are defined
        }

        var result = await _chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, cancellationToken: ct);

        var functionCalls = result.Items
            .OfType<Microsoft.SemanticKernel.FunctionCallContent>()
            .Select(fc => new FunctionCall(fc.FunctionName!, JsonDocument.Parse(JsonSerializer.Serialize(fc.Arguments!)).RootElement.Clone()))
            .ToList();

        return new ChatResult
        {
            Text = result.Content,
            FunctionCalls = functionCalls
        };
    }

    private sealed class ChatResult : IChatResult
    {
        public string? Text { get; init; }
        public IReadOnlyList<FunctionCall> FunctionCalls { get; init; } = Array.Empty<FunctionCall>();
    }
}
