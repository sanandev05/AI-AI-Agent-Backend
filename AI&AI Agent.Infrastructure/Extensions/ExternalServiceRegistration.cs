using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Contract.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Net.Http;

namespace AI_AI_Agent.Infrastructure.Extensions
{
    public static class ExternalServiceRegistration
    {
        public static IServiceCollection AddExternalServiceRegistration(
            this IServiceCollection services, IConfiguration config)
        {
            var openAiKey = config["OpenAI:ApiKey"];
            if (!string.IsNullOrWhiteSpace(openAiKey))
            {
                services.AddKeyedSingleton<IChatCompletionService>("gpt", (sp, _) =>
                    new OpenAIChatCompletionService(
                        modelId: "gpt-4o-mini",
                        apiKey: openAiKey,
                        httpClient: sp.GetRequiredService<HttpClient>(),
                        loggerFactory: sp.GetRequiredService<ILoggerFactory>()
                    )
                );
            }
#pragma warning disable SKEXP0070
            var geminiKey = config["Gemini:ApiKey"];
            if (!string.IsNullOrWhiteSpace(geminiKey))
            {
                services.AddKeyedSingleton<IChatCompletionService>("gemini", (sp, _) =>
                    new GoogleAIGeminiChatCompletionService(
                        modelId: "gemini-2.0-flash",
                        apiKey: geminiKey,
                        apiVersion: GoogleAIVersion.V1,
                        httpClient: sp.GetRequiredService<HttpClient>(),
                        loggerFactory: sp.GetRequiredService<ILoggerFactory>()
                    )
                );
            }

            services.AddTransient(sp => new Kernel(sp));
            services.AddScoped<IChatService, ChatService>();

            return services;
        }
    }
}