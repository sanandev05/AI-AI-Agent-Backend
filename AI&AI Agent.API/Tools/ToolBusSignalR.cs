using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using AI_AI_Agent.Application.Agent;
using AI_AI_Agent.API.Hubs;

namespace AI_AI_Agent.API.Tools;

// Tool bus that emits events via SignalR
public class ToolBusSignalR : IToolBus
{
    private readonly IHubContext<AgentEventsHub> _hub;

    public ToolBusSignalR(IHubContext<AgentEventsHub> hub) => _hub = hub;

    public Task EmitToolStartAsync(string chatId, int step, string tool, object args) =>
        _hub.Clients.Group(chatId).SendAsync("tool:start", new { chatId, step, tool, args });

    public Task EmitToolEndAsync(string chatId, int step, string tool, object result) =>
        _hub.Clients.Group(chatId).SendAsync("tool:end", new { chatId, step, tool, result });

    public Task EmitFinalAsync(string chatId, int step, string text) =>
        _hub.Clients.Group(chatId).SendAsync("final:answer", new { chatId, step, text });

    public Task EmitStepStartAsync(string chatId, int step, string userPrompt, int historyCount) =>
        _hub.Clients.Group(chatId).SendAsync("step:start", new { chatId, step, userPrompt, historyCount });

    public Task EmitRawModelAsync(string chatId, int step, string rawText) =>
        _hub.Clients.Group(chatId).SendAsync("raw:model", new { chatId, step, rawText });

    public Task EmitFileCreatedAsync(string chatId, int step, string fileName, string downloadUrl, long sizeBytes) =>
        _hub.Clients.Group(chatId).SendAsync("file:created", new { chatId, step, fileName, downloadUrl, sizeBytes });

    public Task EmitPlanCreatedAsync(string chatId, object plan) =>
        _hub.Clients.Group(chatId).SendAsync("plan:created", new { chatId, plan });

    public Task EmitPlanUpdatedAsync(string chatId, object plan) =>
        _hub.Clients.Group(chatId).SendAsync("plan:updated", new { chatId, plan });

    public Task EmitTimelineAsync(string chatId, string kind, string message, object? data = null) =>
        _hub.Clients.Group(chatId).SendAsync("timeline:log", new { chatId, kind, message, data });
}
