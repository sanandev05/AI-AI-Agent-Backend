using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Contracts;

namespace AI_AI_Agent.Application.Agent.Storage;

public interface IChatStore
{
    Task<IReadOnlyList<ChatMessageContent>> LoadHistoryAsync(string chatId, CancellationToken ct = default);
    Task AppendUserAsync(string chatId, string content, CancellationToken ct = default);
    Task AppendAssistantAsync(string chatId, string content, CancellationToken ct = default);
    Task AppendToolResultAsync(string chatId, string toolName, string result, CancellationToken ct = default);
}