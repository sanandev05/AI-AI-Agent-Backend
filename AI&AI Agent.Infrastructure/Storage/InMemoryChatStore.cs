using AI_AI_Agent.Application.Agent.Storage;
using AI_AI_Agent.Contracts;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Infrastructure.Storage;

public class InMemoryChatStore : IChatStore
{
    private static readonly ConcurrentDictionary<string, List<ChatMessageContent>> _chats = new();

    public Task<IReadOnlyList<ChatMessageContent>> LoadHistoryAsync(string chatId, CancellationToken ct = default)
    {
        if (_chats.TryGetValue(chatId, out var history))
        {
            return Task.FromResult<IReadOnlyList<ChatMessageContent>>(history.ToList());
        }
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent>());
    }

    public Task AppendUserAsync(string chatId, string content, CancellationToken ct = default)
    {
        var history = _chats.GetOrAdd(chatId, new List<ChatMessageContent>());
        history.Add(new ChatMessageContent("user", content));
        return Task.CompletedTask;
    }

    public Task AppendAssistantAsync(string chatId, string content, CancellationToken ct = default)
    {
        var history = _chats.GetOrAdd(chatId, new List<ChatMessageContent>());
        history.Add(new ChatMessageContent("assistant", content));
        return Task.CompletedTask;
    }

    public Task AppendToolResultAsync(string chatId, string toolName, string result, CancellationToken cancellationToken)
    {
        var history = GetHistory(chatId);
        // Store tool output as a chat message with role "tool" and include tool name for traceability
        var content = string.IsNullOrWhiteSpace(toolName) ? result : $"[{toolName}] {result}";
        history.Add(new ChatMessageContent("tool", content));
        return Task.CompletedTask;
    }

    private static List<ChatMessageContent> GetHistory(string chatId)
    {
        return _chats.GetOrAdd(chatId, new List<ChatMessageContent>());
    }
}
