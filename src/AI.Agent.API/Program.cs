using AI.Agent.Application;
using AI.Agent.Application.Budgets;
using AI.Agent.Application.Critic;
using AI.Agent.Application.Executor;
using AI.Agent.Domain.Memory;
using AI.Agent.Infrastructure.Memory;
using AI.Agent.Application.Planner;
using AI.Agent.Application.Routing;
using AI.Agent.Application.Tools;
using AI_AI_Agent.API.Hubs;
using AI_AI_Agent.API.Eventing;
using AI.Agent.Infrastructure.Storage;
using AI.Agent.Infrastructure.Stores;
using Microsoft.SemanticKernel;
// duplicate using removed
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    // Ensure camelCase and include $type discriminators where possible
    options.PayloadSerializerOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
});
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS (optional for local file testing, otherwise same-origin works)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true));
});

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-change-me";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ai-agent";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ai-agent-clients";
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        // Allow SignalR to pass token via access_token
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub/runs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Add Semantic Kernel with default configuration
var kernelBuilder = builder.Services.AddKernel();

// Add OpenAI as default (you can configure this via appsettings.json)
var openAiKey = builder.Configuration["OpenAI:ApiKey"];
var openAiModel = builder.Configuration["OpenAI:ModelId"] ?? "gpt-4o";
var embeddingModel = builder.Configuration["OpenAI:EmbeddingModelId"] ?? "text-embedding-3-small";

if (!string.IsNullOrWhiteSpace(openAiKey))
{
    kernelBuilder.AddOpenAIChatCompletion(openAiModel, openAiKey);
}
else
{
    Console.WriteLine("Warning: No OpenAI API key configured. Set OpenAI:ApiKey in appsettings.json");
    // Add a fallback null kernel for testing
    builder.Services.AddSingleton<Kernel>(sp => Kernel.CreateBuilder().Build());
}

// DI
builder.Services.AddSingleton<IEventBus, SignalREventBus>();
builder.Services.AddSingleton<IArtifactStore, FileArtifactStore>();
builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
builder.Services.AddSingleton<IBudgetManager, BudgetManager>();
builder.Services.AddSingleton<ICritic, SimpleCritic>();
builder.Services.AddSingleton<IApprovalGate, InMemoryApprovalGate>();

// Register all browser tools
builder.Services.AddSingleton<ITool, BrowserSearchTool>();
builder.Services.AddSingleton<ITool, BrowserGotoTool>();
builder.Services.AddSingleton<ITool, BrowserScreenshotTool>();
builder.Services.AddSingleton<ITool, BrowserExtractTool>();
builder.Services.AddSingleton<ITool, BrowserScrollTool>();
builder.Services.AddSingleton<ITool, BrowserClickTool>();

// Register analysis and creation tools
builder.Services.AddSingleton<ITool, SummarizeTool>();
builder.Services.AddSingleton<ITool, DocxCreateTool>();
builder.Services.AddSingleton<ITool, LlmAnswerTool>();

// Register memory store and tools (RAG-lite)
builder.Services.AddSingleton<IMemoryStore>(sp =>
{
    var dataDir = Path.Combine(AppContext.BaseDirectory, "data");
    Directory.CreateDirectory(dataDir);
    var path = Path.Combine(dataDir, "memory.json");
    return new FileMemoryStore(path);
});

// Embedding function: simple hashing fallback (keeps build stable without SK embeddings)
builder.Services.AddSingleton<Func<string, CancellationToken, Task<float[]>>>(_ =>
    (text, ct) => Task.FromResult(HashEmbedding(text))
);

static float[] HashEmbedding(string text)
{
    var v = new float[128];
    unchecked
    {
        foreach (var ch in text)
        {
            int idx = ch % 128;
            v[idx] += 1f;
        }
    }
    // L2 normalize
    double norm = Math.Sqrt(v.Sum(x => x * x));
    if (norm > 0) for (int i = 0; i < v.Length; i++) v[i] = (float)(v[i] / norm);
    return v;
}
builder.Services.AddSingleton<ITool, AI.Agent.Application.Tools.Memory.MemoryAddTool>();
builder.Services.AddSingleton<ITool, AI.Agent.Application.Tools.Memory.MemorySearchTool>();

// Register orchestration services
builder.Services.AddSingleton<IToolRouter, ToolRouter>();
builder.Services.AddSingleton<IPlanner, JsonPlanner>();
builder.Services.AddSingleton<IExecutor, Executor>();

var app = builder.Build();

app.UseRouting();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles(); // Serve index.html at /
app.UseStaticFiles(); // Enable static files for any test pages
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RunHub>("/hub/runs").RequireAuthorization();

Console.WriteLine("🚀 AI Agent API started");
Console.WriteLine("📋 Available tools:");
var tools = app.Services.GetServices<ITool>();
foreach (var tool in tools)
{
    Console.WriteLine($"   - {tool.Name}");
}

app.Run();
