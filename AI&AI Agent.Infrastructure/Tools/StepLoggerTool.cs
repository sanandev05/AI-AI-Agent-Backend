using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AI_AI_Agent.Application.Agent;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Tools;

public sealed class StepLoggerTool : ITool
{
    public string Name => "StepLogger";
    public string Description => "Emits Manus-style timeline log phrases via SignalR for real-time user feedback.";
    public object JsonSchema => new
    {
        type = "object",
        properties = new
        {
            action = new { type = "string", description = "Action type: 'searching', 'browsing', 'file_issue', 'viewing_image', 'executing', 'creating_file'" },
            message = new { type = "string", description = "Custom message (optional, will use default for action)" },
            url = new { type = "string", description = "URL for browsing action" },
            path = new { type = "string", description = "File path for viewing_image action" },
            name = new { type = "string", description = "File name for creating_file action" },
            expr = new { type = "string", description = "Expression for executing action" }
        },
        required = new[] { "action" }
    };

    private readonly IToolBus _toolBus;
    private readonly ILogger<StepLoggerTool> _logger;

    public StepLoggerTool(IToolBus toolBus, ILogger<StepLoggerTool> logger)
    {
        _toolBus = toolBus;
        _logger = logger;
    }

    public async Task<object> InvokeAsync(JsonElement args, CancellationToken ct)
    {
        if (!args.TryGetProperty("action", out var actionProp) || actionProp.ValueKind != JsonValueKind.String)
            return new { error = "action is required", success = false };

        var action = actionProp.GetString()?.ToLowerInvariant() ?? "";
        var customMessage = args.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
        var url = args.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : null;
        var path = args.TryGetProperty("path", out var pathProp) ? pathProp.GetString() : null;
        var name = args.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
        var expr = args.TryGetProperty("expr", out var exprProp) ? exprProp.GetString() : null;

        // Get current chat context (this would need to be passed in real implementation)
        var chatId = "current-chat"; // TODO: Get from context

        string timelineMessage;
        string logKind;

        if (!string.IsNullOrEmpty(customMessage))
        {
            timelineMessage = customMessage;
            logKind = "custom";
        }
        else
        {
            (timelineMessage, logKind) = action switch
            {
                "searching" => ("Searching …", "searching"),
                "browsing" => ($"Browsing {url ?? "page"}", "browsing"),
                "file_issue" => ("Handling file reading issue", "file_issue"),
                "viewing_image" => ($"Viewing image {path ?? "file"}", "viewing_image"),
                "executing" => ($"Executing command {expr ?? "operation"}", "executing"),
                "creating_file" => ($"Creating file {name ?? "document"}", "creating_file"),
                _ => ($"Processing {action}", "general")
            };
        }

        try
        {
            // Emit timeline event via SignalR
            await _toolBus.EmitTimelineAsync(chatId, logKind, timelineMessage, new { 
                action, 
                url, 
                path, 
                name, 
                expr,
                timestamp = System.DateTime.UtcNow 
            });

            _logger.LogInformation("Timeline log emitted: {Message} (action: {Action})", timelineMessage, action);

            return new
            {
                success = true,
                action,
                message = timelineMessage,
                kind = logKind,
                emitted = "Timeline log sent via SignalR"
            };
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to emit timeline log for action: {Action}", action);
            return new { error = ex.Message, success = false };
        }
    }
}