using AI_AI_Agent.API.Hubs;
using AI_AI_Agent.API.JWT;
using AI_AI_Agent.API.Options;
using AI_AI_Agent.Application.Agent.Routing;
using AI_AI_Agent.Application.Agent.Routing.Backends;
using AI_AI_Agent.Application.Extensions;
using AI_AI_Agent.Application.Services;
using AI_AI_Agent.Infrastructure.Extensions;
using AI_AI_Agent.Persistance.AppDbContext;
using AI_AI_Agent.Persistance.Extensions;
using AI_AI_Agent.Infrastructure.Orchestration.Storage;
using AI_AI_Agent.Infrastructure.Orchestration.Stores;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using AI_AI_Agent.Application;
using AI_AI_Agent.Application.Budgets;
using AI_AI_Agent.Application.Critic;
using AI_AI_Agent.Application.Executor;
using AI_AI_Agent.Application.Planner;
using AI_AI_Agent.Application.Routing;
using AI_AI_Agent.Application.Tools;
using Microsoft.SemanticKernel.Connectors.Google;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactFrontWebApp",
        policy =>
        {
            policy
                .WithOrigins(
                    // React (CRA)
                    "http://localhost:3000", "https://localhost:3000",
                    "http://localhost:3001", "https://localhost:3001",
                    // Vite default
                    "http://localhost:5173", "https://localhost:5173",
                    "http://127.0.0.1:5173", "https://127.0.0.1:5173",
                    // Angular default
                    "http://localhost:4200", "https://localhost:4200"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                // Also allow any loopback origin (covers other localhost ports)
                .SetIsOriginAllowed(origin =>
                {
                    if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                        return uri.IsLoopback;
                    return false;
                });

        });
});

// Bind Configuration
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection(AgentSettings.SectionName));

// Note: Use a single JWT bearer scheme to avoid 'Scheme already exists: Bearer' conflicts.

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Define Bearer scheme and apply as a global requirement so Swagger UI sends Authorization on all calls
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Paste ONLY the raw JWT token below (no 'Bearer ' prefix)."
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddDbContext<AIDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AIDbContext>()
    .AddDefaultTokenProviders();

// JWT Config
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var jwtKey = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        throw new InvalidOperationException("JWT key (Jwt:Key) is not configured. Set it in appsettings or user-secrets.");
    }
    var jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
    if (jwtKeyBytes.Length < 32)
    {
        throw new InvalidOperationException($"JWT key (Jwt:Key) is too short for HS256. It must be at least 256 bits (32 bytes). Current: {jwtKeyBytes.Length * 8} bits. Provide a longer random secret (32+ characters).");
    }
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes),
        ClockSkew = TimeSpan.FromMinutes(2)
    };

    // Add verbose diagnostics to understand 401 causes
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var hasAuth = context.Request.Headers.ContainsKey("Authorization");
            var method = context.Request.Method;
            var path = context.Request.Path.ToString();
            var scheme = context.Request.Scheme;
            // Support SignalR WebSocket auth via access_token query param
            var accessToken = context.Request.Query["access_token"].ToString();
            var isHub = path.StartsWith("/hubs/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/hub/", StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(accessToken) && (path.StartsWith("/hubs/agent-events") || path.StartsWith("/hub/runs")))
            {
                context.Token = accessToken;
                hasAuth = true;
            }
            // Suppress noisy logs for SignalR negotiate/connect without auth; these hubs may allow anonymous
            var isNegotiateOrWs = string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase) && path.EndsWith("/negotiate", StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase);
            if (!(isHub && isNegotiateOrWs && !hasAuth))
            {
                Console.WriteLine($"[JWT] OnMessageReceived: {(hasAuth ? "Authorization present" : "Authorization missing")} for {method} {scheme}://{context.Request.Host}{path}");
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"[JWT] AuthenticationFailed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var sub = context.Principal?.FindFirst("sub")?.Value
                      ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            Console.WriteLine($"[JWT] TokenValidated: sub/nameid={sub ?? "null"}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"[JWT] Challenge: error={context.Error}, desc={context.ErrorDescription}");
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddScoped<JwtTokenGenerator>();

builder.Services.AddAuthorization();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Password settings.
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequiredUniqueChars = 1;

    // Lockout settings.
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;


    options.User.RequireUniqueEmail = true;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // Cookie settings
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
});



builder.Services.AddHttpClient();
builder.Services.AddScoped<ManusAgent>();
builder.Services.AddServiceRegistration();
builder.Services.AddRepositoryRegistration();
builder.Services.AddExternalServiceRegistration(builder.Configuration);
builder.Services.AddSingleton<AI_AI_Agent.Application.Agent.Storage.IChatStore, AI_AI_Agent.Infrastructure.Storage.InMemoryChatStore>();
builder.Services.AddAgentServices();
builder.Services.AddAdvancedAgentServices(); // Phase 2 & 3: Advanced agent capabilities
builder.Services.AddAgentTools();

// Add Semantic Kernel with configured backends (robust validation + fallback)
var agentSettings = builder.Configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>();
var kernelBuilder = builder.Services.AddKernel();

bool anyDefaultChatSet = false;
if (agentSettings?.Backends is not null && agentSettings.Backends.Count > 0)
{
    foreach (var (serviceId, cfg) in agentSettings.Backends)
    {
        // Normalize provider string
        var provider = (cfg.Provider ?? "AzureOpenAI").Trim();
        var modelId = (cfg.ModelId ?? string.Empty).Trim();
        var apiKey = (cfg.ApiKey ?? string.Empty).Trim();
        var endpoint = (cfg.Endpoint ?? string.Empty).Trim();

        try
        {
            switch (provider.ToLowerInvariant())
            {
                case "azureopenai":
                case "azure-openai":
                case "azure":
                {
                    if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(endpoint))
                    {
                        Console.WriteLine($"[Agent] Skipping AzureOpenAI backend '{serviceId}': missing ModelId, ApiKey, or Endpoint.");
                        break;
                    }
                    if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    {
                        Console.WriteLine($"[Agent] Skipping AzureOpenAI backend '{serviceId}': Endpoint is not a valid absolute URI.");
                        break;
                    }

                    // Register keyed service for routing
                    kernelBuilder.AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey, serviceId: serviceId);

                    // Set the first valid provider as default
                    if (!anyDefaultChatSet)
                    {
                        kernelBuilder.AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);
                        anyDefaultChatSet = true;
                    }

                    builder.Services.AddSingleton<IChatBackend>(sp =>
                        new AzureOpenAIChatBackend(serviceId, sp.GetRequiredService<Kernel>()));
                    break;
                }
                case "openai":
                {
                    if (string.IsNullOrWhiteSpace(modelId) || string.IsNullOrWhiteSpace(apiKey))
                    {
                        Console.WriteLine($"[Agent] Skipping OpenAI backend '{serviceId}': missing ModelId or ApiKey.");
                        break;
                    }

                    kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey, serviceId: serviceId);
                    if (!anyDefaultChatSet)
                    {
                        kernelBuilder.AddOpenAIChatCompletion(modelId, apiKey);
                        anyDefaultChatSet = true;
                    }

                    builder.Services.AddSingleton<IChatBackend>(sp =>
                        new AzureOpenAIChatBackend(serviceId, sp.GetRequiredService<Kernel>()));
                    break;
                }
                default:
                    Console.WriteLine($"[Agent] Skipping backend '{serviceId}': unsupported provider '{provider}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            // Guard against misconfiguration exceptions during DI setup
            Console.WriteLine($"[Agent] Failed to register backend '{serviceId}' ({provider}): {ex.Message}");
        }
    }
}

// Also register direct OpenAI/Gemini models from top-level config so keyed model selection works
var openAiKey = builder.Configuration["OpenAI:ApiKey"]?.Trim();
var geminiKey = builder.Configuration["Google:ApiKey"]?.Trim();
if (!string.IsNullOrWhiteSpace(openAiKey))
{
    // Ensure at least common OpenAI keyed models are available by id
    var openAiModels = new[] { "gpt-4o", "gpt-4o-mini" };
    foreach (var m in openAiModels)
    {
        try { kernelBuilder.AddOpenAIChatCompletion(m, openAiKey, serviceId: m); } catch { }
    }
}
if (!string.IsNullOrWhiteSpace(geminiKey))
{
    // Register Gemini keyed services by model id and provider-keyed default "Google"
    var geminiModels = new[] { "gemini-2.5-pro", "gemini-2.5-flash", "gemini-1.5-flash" };
    foreach (var m in geminiModels)
    {
        try { kernelBuilder.AddGoogleAIGeminiChatCompletion(m, geminiKey, serviceId: m); } catch { }
    }
    try { kernelBuilder.AddGoogleAIGeminiChatCompletion("gemini-1.5-flash", geminiKey, serviceId: "Google"); } catch { }
}

// Global fallback if no valid backend set any default chat service
if (!anyDefaultChatSet)
{
    var fallbackKey = builder.Configuration["OpenAI:ApiKey"]?.Trim();
    var fallbackModel = builder.Configuration["OpenAI:ModelId"]?.Trim();
    if (!string.IsNullOrWhiteSpace(fallbackKey))
    {
        // Prefer provided model, otherwise pick a sensible default
        var model = string.IsNullOrWhiteSpace(fallbackModel) ? "gpt-4o" : fallbackModel!;
        Console.WriteLine("[Agent] No valid backends configured. Using OpenAI fallback from OpenAI:ApiKey.");
        kernelBuilder.AddOpenAIChatCompletion(model, fallbackKey);
        anyDefaultChatSet = true;
        // Also expose via routing with a conventional serviceId
        var serviceId = "OpenAI:Default";
        kernelBuilder.AddOpenAIChatCompletion(model, fallbackKey, serviceId: serviceId);
        builder.Services.AddSingleton<IChatBackend>(sp => new AzureOpenAIChatBackend(serviceId, sp.GetRequiredService<Kernel>()));
    }
    else
    {
        Console.WriteLine("[Agent] WARNING: No chat completion service configured. Set Agent:Backends or OpenAI:ApiKey.");
    }
}


// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// IToolBus
builder.Services.AddSingleton<AI_AI_Agent.Application.Agent.IToolBus, AI_AI_Agent.API.Tools.ToolBusSignalR>();

// Orchestration DI
// Expose FileArtifactStore concrete for controllers that depend on it directly, and map IArtifactStore to the same instance
builder.Services.AddSingleton<FileArtifactStore>();
builder.Services.AddSingleton<IArtifactStore>(sp => sp.GetRequiredService<FileArtifactStore>());
builder.Services.AddSingleton<IRunStore, InMemoryRunStore>();
builder.Services.AddSingleton<IBudgetManager, BudgetManager>();
builder.Services.AddSingleton<ICritic, SimpleCritic>();
builder.Services.AddSingleton<InMemoryApprovalGate>();
builder.Services.AddSingleton<IApprovalGate>(sp => sp.GetRequiredService<InMemoryApprovalGate>());
builder.Services.AddSingleton<IApprovalCoordinator>(sp => sp.GetRequiredService<InMemoryApprovalGate>());

// Register concrete tool implementations
builder.Services.AddSingleton<BrowserSearchTool>();
builder.Services.AddSingleton<BrowserExtractTool>();
builder.Services.AddSingleton<BrowserScreenshotTool>();
builder.Services.AddSingleton<SummarizeTool>();
builder.Services.AddSingleton<AnswerTool>();
builder.Services.AddSingleton<DecideExternalTool>();
builder.Services.AddSingleton<DocxCreateTool>();
builder.Services.AddSingleton<ChartCreateTool>();
builder.Services.AddSingleton<SearchApiTool>();
builder.Services.AddSingleton<PptxCreateTool>();
builder.Services.AddSingleton<WebContentFetchTool>();
builder.Services.AddSingleton<WebResearchAgentTool>();

// Register as ITool interface (for tool collection)
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<BrowserSearchTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<BrowserExtractTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<BrowserScreenshotTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<SummarizeTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<AnswerTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<DecideExternalTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<DocxCreateTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<ChartCreateTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<SearchApiTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<PptxCreateTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<WebContentFetchTool>());
builder.Services.AddSingleton<ITool>(sp => sp.GetRequiredService<WebResearchAgentTool>());

builder.Services.AddSingleton<IToolRouter, ToolRouter>();
builder.Services.AddSingleton<IPlanner, JsonPlanner>();
builder.Services.AddSingleton<IEventBus, AI_AI_Agent.API.Eventing.SignalREventBus>();
builder.Services.AddSingleton<IExecutor, Executor>();

var app = builder.Build();

app.UseCors("AllowReactFrontWebApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c =>
    {
        c.PreSerializeFilters.Add((swagger, httpReq) =>
        {
            swagger.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
            {
                new Microsoft.OpenApi.Models.OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}" }
            };
        });
    });
    app.UseSwaggerUI();
    // Ensure the generated OpenAPI document uses the incoming request's scheme/host to avoid http->https redirects that can drop headers
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            // No-op middleware hook point; actual server URL override is configured below
        }
        await next();
    });
}


app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AgentEventsHub>("/hubs/agent-events").AllowAnonymous();
app.MapHub<RunHub>("/hub/runs").AllowAnonymous();

app.Run();
