using System.Threading;
using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent;

public interface IOrchestrator
{
    Task RunAsync(string chatId, string userPrompt, CancellationToken cancellationToken = default);
}
