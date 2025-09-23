using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Contract.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Embeddings;
using AI_AI_Agent.Domain.Agents;
using System.Net.Http;
using AI_AI_Agent.Application.Services;

namespace AI_AI_Agent.Infrastructure.Extensions
{
    public static class ExternalServiceRegistration
    {
        public static IServiceCollection AddExternalServiceRegistration(
            this IServiceCollection services, IConfiguration config)
        {
            var openAiKey = config["OpenAI:ApiKey"];
            var geminiKey = config["Google:ApiKey"];
            var googleApiKey = config["Google:ApiKey"];
            var googleSearchEngineId = config["Google:SearchEngineId"];

            // Register GoogleSearchService as IGoogleSearchService
            services.AddScoped<IGoogleSearchService, Services.GoogleSearchService>();

            // Semantic Kernel setup
            services.AddSingleton<IMemoryStore, VolatileMemoryStore>();
            services.AddOpenAIEmbeddingGenerator("text-embedding-ada-002", openAiKey);
            services.AddScoped<ISemanticTextMemory, SemanticTextMemory>();

            services.AddScoped(sp =>
            {
                var builder = Kernel.CreateBuilder();
                builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));

                // Add Chat Completion Services
                builder.AddOpenAIChatCompletion("gpt-4o", openAiKey, serviceId: "OpenAI");
                builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", geminiKey, serviceId: "Google");

                // Add Memory and Plugins
                builder.Services.AddSingleton(sp.GetRequiredService<IMemoryStore>());
                
                return builder.Build();
            });


            services.AddScoped<IChatService>(sp => new ChatService(
                sp,
                sp.GetRequiredService<Kernel>(),
                sp.GetRequiredService<IGenericService<Contract.DTOs.MessageDto, Domain.Entities.Message>>(),
                sp.GetRequiredService<Domain.Repositories.IChatRepository>(),
                sp.GetRequiredService<AutoMapper.IMapper>(),
                sp.GetRequiredService<ILogger<ChatService>>(),
                sp.GetRequiredService<IWebHostEnvironment>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                sp.GetRequiredService<IGoogleSearchService>()
            ));

            services.AddHttpContextAccessor();

    

            return services;
        }
    }
}