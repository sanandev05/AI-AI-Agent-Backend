using AI_AI_Agent.Contracts;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent.Routing;

public interface IChatBackend
{
    string Name { get; }

    // toolSchema can be a function-tools array you pass to the provider
    Task<IChatResult> CompleteAsync(
        string system,
        string user,
        IEnumerable<ChatMessageContent> history,
        object? toolSchema,
        CancellationToken ct = default);
}
