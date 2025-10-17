using AI_AI_Agent.Application;
using Microsoft.AspNetCore.SignalR;

namespace AI_AI_Agent.API.Eventing;

public sealed class SignalREventBus : IEventBus
{
    private readonly IHubContext<AI_AI_Agent.API.Hubs.RunHub> _hub;
    public SignalREventBus(IHubContext<AI_AI_Agent.API.Hubs.RunHub> hub)
    { _hub = hub; }

    public Task PublishAsync(object evt, CancellationToken ct = default)
    {
        var runIdProp = evt.GetType().GetProperty("RunId");
        var runId = (Guid?)runIdProp?.GetValue(evt);
        var group = runId?.ToString() ?? "default";
        return _hub.Clients.Group(group).SendAsync("event", evt, ct);
    }
}
