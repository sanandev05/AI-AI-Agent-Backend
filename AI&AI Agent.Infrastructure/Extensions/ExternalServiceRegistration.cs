using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Contract.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;

namespace AI_AI_Agent.Infrastructure.Extensions
{
    public static class ExternalServiceRegistration
    {
        public static IServiceCollection AddExternalServiceRegistration(this IServiceCollection services, IConfiguration config)
        {
            services.AddOpenAIChatCompletion(
                modelId: "gpt-3.5-turbo",
                apiKey: config["AI_API_KEYS:OpenAI"]
            );


            #pragma warning disable SKEXP0070
            services.AddGoogleAIGeminiChatCompletion(
                modelId: "NAME_OF_MODEL",
                apiKey: "API_KEY",
                apiVersion: GoogleAIVersion.V1, // Optional
                serviceId: "SERVICE_ID" // Optional; for targeting specific services within Semantic Kernel
            );

        
            services.AddTransient(sp => new Kernel(sp));

            services.AddScoped<IChatService, ChatService>();

            return services;
        }
    }
}
