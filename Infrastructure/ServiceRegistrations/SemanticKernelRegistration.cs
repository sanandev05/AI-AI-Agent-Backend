using AI_AI_Agent.Domain.Agents;
using AI_AI_Agent.Domain.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AI_AI_Agent.Infrastructure.ServiceRegistrations
{
    public static class SemanticKernelRegistration
    {
        // Call in your existing external services extension: services.AddSemanticKernelAgent(Configuration);
        public static IServiceCollection AddSemanticKernelAgent(this IServiceCollection services, IConfiguration config)
        {
            var provider = config.GetValue<string>("AI:Provider") ?? "OpenAI";
            // OpenAI settings
            var openAiModel = config.GetValue<string>("AI:OpenAI:ChatModel");
            var openAiKey = config.GetValue<string>("AI:OpenAI:ApiKey");

            // Azure OpenAI settings
            var azureEndpoint = config.GetValue<string>("AI:Azure:Endpoint");
            var azureDeployment = config.GetValue<string>("AI:Azure:Deployment");
            var azureKey = config.GetValue<string>("AI:Azure:ApiKey");

            services.AddKernel(builder =>
            {
                if (string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase))
                {
                    builder.AddAzureOpenAIChatCompletion(
                        deploymentName: azureDeployment ?? throw new InvalidOperationException("Missing AI:Azure:Deployment"),
                        endpoint: azureEndpoint ?? throw new InvalidOperationException("Missing AI:Azure:Endpoint"),
                        apiKey: azureKey ?? throw new InvalidOperationException("Missing AI:Azure:ApiKey"));
                }
                else
                {
                    builder.AddOpenAIChatCompletion(
                        modelId: openAiModel ?? throw new InvalidOperationException("Missing AI:OpenAI:ChatModel"),
                        apiKey: openAiKey ?? throw new InvalidOperationException("Missing AI:OpenAI:ApiKey"));
                }

                // Register plugins (can be toggled later)
                builder.Plugins.AddFromObject(new DateTimePlugin(), "datetime");
            });

            services.AddSingleton<IAgent, GeneralAgent>();

            return services;
        }
    }
}
