using AI_AI_Agent.Application.Agent;
using AI_AI_Agent.Application.Agent.Routing;
using AI_AI_Agent.Application.Agent.Routing.Backends;
using AI_AI_Agent.Application.Agent.Storage;
using AgentPlanning = AI_AI_Agent.Application.Agent.Planning;
using Microsoft.Extensions.DependencyInjection;

namespace AI_AI_Agent.Application.Extensions;

public static class AgentServiceRegistration
{
    public static IServiceCollection AddAgentServices(this IServiceCollection services)
    {
        // Register Agent Core Loop
    // Use singleton for agent components to avoid disposed scope issues during background runs
    services.AddScoped<AgentLoop>();
    services.AddScoped<IOrchestrator, SequentialOrchestrator>();

        // Register LLM Router and Backends
        services.AddSingleton<LLMRouter>();
        // This part will be configured from Program.cs based on settings
        // services.AddSingleton<IChatBackend>(sp => new AzureOpenAIChatBackend("Reasoning", sp.GetRequiredService<Kernel>()));
        // services.AddSingleton<IChatBackend>(sp => new AzureOpenAIChatBackend("Cheap", sp.GetRequiredService<Kernel>()));

        // Register Storage (the implementation is in Infrastructure)
        // This is now registered in the API project to break the circular dependency.
        // services.AddScoped<IChatStore, AI_AI_Agent.Infrastructure.Storage.InMemoryChatStore>();

        // Planning components
    services.AddSingleton<AgentPlanning.IPlanner, AgentPlanning.LlmPlanner>();
    services.AddSingleton<AgentPlanning.IPlanStore, AgentPlanning.InMemoryPlanStore>();

    // Cancellation registry is registered in Infrastructure extensions

        return services;
    }
}
