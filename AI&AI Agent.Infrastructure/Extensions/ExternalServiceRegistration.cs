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
using AI_AI_Agent.Infrastructure.Tools;
using Microsoft.Playwright;
using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Infrastructure.Services;

#pragma warning disable OPENAI001 // Type is for evaluation purposes only

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

            // Register GoogleSearchService as IGoogleSearchService (Singleton to match tool registrations)
            services.AddSingleton<IGoogleSearchService, Services.GoogleSearchService>();
            services.AddSingleton<IUrlSafetyService, UrlSafetyService>();
            services.AddSingleton<IRunCancellationRegistry, RunCancellationRegistry>();
            services.AddSingleton<IApprovalService, ApprovalService>();

            // Semantic Kernel setup
            services.AddSingleton<IMemoryStore, VolatileMemoryStore>();
            
            // Note: ITextEmbeddingGenerationService is obsolete in newer SK versions
            // If embedding functionality is needed, use Microsoft.Extensions.AI.IEmbeddingGenerator<string, Embedding<float>>
            if (!string.IsNullOrWhiteSpace(openAiKey))
            {
                // This adds the embedding generator internally but may not expose ITextEmbeddingGenerationService
                services.AddOpenAIEmbeddingGenerator("text-embedding-ada-002", openAiKey);
            }
            
            services.AddScoped<ISemanticTextMemory, SemanticTextMemory>();

            // Eagerly seed the model registry so /api/models is populated even before Kernel is resolved
            var seededRegistry = new ModelRegistry();
            var openAiModelsForRegistry = new[] { "gpt-5", "gpt-5-mini", "gpt-4o", "gpt-4o-mini", "gpt-4", "gpt-3.5-turbo" };
            foreach (var m in openAiModelsForRegistry)
            {
                seededRegistry.Add("OpenAI", m, m.ToUpperInvariant().Replace('-', ' '), available: !string.IsNullOrWhiteSpace(openAiKey));
            }
            var geminiModelsForRegistry = new[] { "gemini-2.5-pro", "gemini-2.5-flash", "gemini-1.5-flash" };
            foreach (var m in geminiModelsForRegistry)
            {
                seededRegistry.Add("Gemini", m, m.Replace("-", " ", StringComparison.OrdinalIgnoreCase), available: !string.IsNullOrWhiteSpace(geminiKey));
            }
            services.AddSingleton<IModelRegistry>(seededRegistry);
            // No Anthropic HTTP client

            services.AddScoped(sp =>
            {
                var builder = Kernel.CreateBuilder();
                builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
                // Registry is already seeded above; no need to add entries here

                // Add Chat Completion Services (keyed + default when available)
                bool defaultSet = false;
                if (!string.IsNullOrWhiteSpace(openAiKey))
                {
                    // OpenAI family (exposed as keyed services by model id)
                    var openAiModels = new[]
                    {
                        "gpt-5",
                        "gpt-5-mini",
                        "gpt-4o",
                        "gpt-4o-mini",
                        "gpt-4",
                        "gpt-3.5-turbo"
                    };
                    foreach (var m in openAiModels)
                    {
                        builder.AddOpenAIChatCompletion(m, openAiKey, serviceId: m);
                    }
                    // Provider-keyed default for enum fallback
                    builder.AddOpenAIChatCompletion("gpt-4o", openAiKey, serviceId: "OpenAI");
                    if (!defaultSet)
                    {
                        builder.AddOpenAIChatCompletion("gpt-4o", openAiKey); // default
                        defaultSet = true;
                    }
                }
                if (!string.IsNullOrWhiteSpace(geminiKey))
                {
                    // Gemini family
                    var geminiModels = new[]
                    {
                        "gemini-2.5-pro",
                        "gemini-2.5-flash",
                        "gemini-1.5-flash"
                    };
                    foreach (var m in geminiModels)
                    {
                        builder.AddGoogleAIGeminiChatCompletion(m, geminiKey, serviceId: m);
                    }
                    // Provider-keyed default for enum fallback - use 'flash' which is broadly available
                    builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", geminiKey, serviceId: "Google");
                    if (!defaultSet)
                    {
                        builder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", geminiKey);
                        defaultSet = true;
                    }
                }

                // Anthropic (Claude) removed per request: no registration of models

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

        public static IServiceCollection AddAdvancedAgentServices(this IServiceCollection services)
        {
            // Register Agent Registry
            services.AddSingleton<AI_AI_Agent.Domain.Agents.IAgentRegistry, AI_AI_Agent.Infrastructure.Services.Agents.AgentRegistry>();

            // Register AssistantClient from OpenAI SDK
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var apiKey = config["OpenAI:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    throw new InvalidOperationException("OpenAI API Key is not configured");
                }
                var openAIClient = new OpenAI.OpenAIClient(apiKey);
                return openAIClient.GetAssistantClient();
            });

            // Register AssistantAgentService
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Agents.AssistantAgentService>();

            // Register OrchestratorAgent
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Agents.OrchestratorAgent>();

            // Register SpecializedAgentFactory
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Agents.SpecializedAgentFactory>();

            // Register Vector Memory Service (for RAG)
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Memory.VectorMemoryService>();

            // Phase 2 & 3: Advanced Agent Capabilities

            // Task Planning System
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Planning.TaskPlanningService>();

            // Multi-Step Reasoning Engine
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Reasoning.ReasoningEngine>();

            // Decision Making Framework
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.DecisionMaking.DecisionMakingService>();

            // State Management System
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.State.StateManagementService>();

            // Observability Infrastructure
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Observability.ObservabilityService>();

            // Security & Sandboxing
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Security.SecurityService>();

            // Phase 2.2: Enhanced Tool Framework
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Tools.ToolSelectionService>();
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Tools.ToolChainingService>();
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Tools.ToolValidationService>();

            // Phase 2.3: Autonomous Behavior
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Autonomous.AutonomousBehaviorService>();

            // Error Recovery System
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Recovery.ErrorRecoveryService>();

            // Phase 4: User Experience & Integration

            // Phase 4.1: Enhanced Chat Interface
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Chat.EnhancedStreamingService>();
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Chat.ConversationManagementService>();
            
            // Rich Media Support
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Media.RichMediaService>();

            // Phase 4.2: Agent Customization & User Preferences
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Customization.AgentCustomizationService>();
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Customization.UserPreferencesService>();

            // Phase 4.3: Multi-Agent Collaboration
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Collaboration.AgentCollaborationService>();
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Collaboration.TeamAgentService>();

            // Phase 5: Advanced Features (Manus-like & GPT Agent)

            // Phase 5.1: Manus AI Agent Features
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.Workspace.WorkspaceManagementService>();
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.ProjectAnalysis.ProjectUnderstandingService>();
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Proactive.ProactiveAssistanceService>();

            // Phase 5.2: GPT Agent Advanced Features
            services.AddScoped<AI_AI_Agent.Infrastructure.Services.CodeInterpreter.CodeInterpreterService>();
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.Knowledge.KnowledgeRetrievalService>();
            services.AddSingleton<AI_AI_Agent.Infrastructure.Services.MultiModal.MultiModalService>();

            return services;
        }

        public static IServiceCollection AddAgentTools(this IServiceCollection services)
        {
            // Register Playwright browser instance
            services.AddSingleton(sp =>
            {
                var playwright = Playwright.CreateAsync().GetAwaiter().GetResult();
                return playwright.Chromium.LaunchAsync(new() { Headless = true }).GetAwaiter().GetResult();
            });

            // ===== CORE TOOLS (Phase 1-3) =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, WebSearchTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, WebBrowserTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, PdfReadTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ExtractorTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, CalculatorTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, FileWriterTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, StepLoggerTool>();

            // ===== FILE CREATION & DOCUMENT TOOLS (Phase 4-5) =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, DocxCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, DocxReadTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, PdfCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, PptxCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ExcelReadTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ExcelCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, PdfToDocxTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, CsvToXlsxTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, CsvAnalyzeTool>();
            
            // ===== DATA ANALYSIS & VISUALIZATION TOOLS =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, DataAnalyzeTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ChartCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, PdfSummarizerTool>();
            
            // ===== PRODUCTIVITY TOOLS =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, CalendarCreateTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, CalendarListTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, EmailDraftTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, EmailSendTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, TasksTool>();
            
            // ===== ADVANCED ANALYSIS TOOLS =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ResearchSummarizeTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ProductCompareTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, FinanceRevenueTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, ImageTextExtractTool>();
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, AudioTranscribeTool>();
            
            // ===== LANGUAGE & TRANSLATION TOOLS =====
            services.AddSingleton<AI_AI_Agent.Application.Agent.ITool, TranslateTool>();

            return services;
        }
    }
}
