using System.Threading.Tasks;

namespace AI_AI_Agent.Application.Agent;

// Event bus abstraction the agent uses to emit user-facing updates
public interface IToolBus
{
    Task EmitToolStartAsync(string chatId, int step, string tool, object args);
    Task EmitToolEndAsync(string chatId, int step, string tool, object result);
    Task EmitFinalAsync(string chatId, int step, string text);
    Task EmitStepStartAsync(string chatId, int step, string userPrompt, int historyCount);
    Task EmitRawModelAsync(string chatId, int step, string rawText);
    Task EmitFileCreatedAsync(string chatId, int step, string fileName, string downloadUrl, long sizeBytes);
    Task EmitPlanCreatedAsync(string chatId, object plan);
    Task EmitPlanUpdatedAsync(string chatId, object plan);
    Task EmitTimelineAsync(string chatId, string kind, string message, object? data = null);
}
