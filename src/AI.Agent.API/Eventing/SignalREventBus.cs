using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using AI.Agent.Application.Executor;

namespace AI_AI_Agent.API.Eventing;

public sealed class SignalREventBus : AI.Agent.Application.IEventBus
{
    private readonly IHubContext<AI_AI_Agent.API.Hubs.RunHub> _hub;

    public SignalREventBus(IHubContext<AI_AI_Agent.API.Hubs.RunHub> hub)
    {
        _hub = hub;
    }

    public Task PublishAsync(object evt, CancellationToken ct = default)
    {
        var runIdProp = evt.GetType().GetProperty("RunId");
        var runId = (Guid?)runIdProp?.GetValue(evt);
        var group = runId?.ToString() ?? "default";

        // Envelope to include $type and camelCase properties
        object Envelope(object e)
        {
            var typeName = e.GetType().Name;
            if (e is AgentNarration an)
            {
                return new Dictionary<string, object?>
                {
                    ["$type"] = typeName,
                    ["runId"] = an.RunId,
                    ["stepId"] = an.StepId,
                    ["message"] = an.Message
                };
            }

            var dict = new Dictionary<string, object?> { ["$type"] = typeName };
            foreach (var p in e.GetType().GetProperties())
            {
                var name = char.ToLowerInvariant(p.Name[0]) + p.Name.Substring(1);
                dict[name] = p.GetValue(e);
            }
            return dict;
        }

        var payload = Envelope(evt);

        // Special handling for narration events to ensure they're prominently displayed
        if (evt is AgentNarration narration)
        {
            var narrationObj = new { runId = narration.RunId, stepId = narration.StepId, message = narration.Message };
            var narrationTask = _hub.Clients.Group(group).SendAsync("narration", narrationObj, ct);
            var eventTask = _hub.Clients.Group(group).SendAsync("event", payload, ct);
            return Task.WhenAll(narrationTask, eventTask);
        }

        return _hub.Clients.Group(group).SendAsync("event", payload, ct);
    }
}
