using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI_AI_Agent.Application.Agent;

// A sequential, pipeline-style orchestrator that structures the agent run into explicit steps
public class SequentialOrchestrator : IOrchestrator
{
    private readonly IServiceProvider _serviceProvider;

    public SequentialOrchestrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task RunAsync(string chatId, string userPrompt, CancellationToken cancellationToken = default)
    {
        // Create a new scope for this long-running operation.
        // This ensures that scoped services (like DbContext) live for the entire duration of the agent run,
        // preventing ObjectDisposedException when the original HTTP request scope is closed.
        using (var scope = _serviceProvider.CreateScope())
        {
            var agentLoop = scope.ServiceProvider.GetRequiredService<AgentLoop>();
            await agentLoop.RunAsync(chatId, userPrompt, cancellationToken);
        }
    }
}
