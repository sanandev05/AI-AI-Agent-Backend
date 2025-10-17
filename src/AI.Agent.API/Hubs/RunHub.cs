using Microsoft.AspNetCore.SignalR;
using AI.Agent.Application;
using AI.Agent.Domain.Events;

namespace AI_AI_Agent.API.Hubs;

public sealed class RunHub : Hub
{
    private readonly IApprovalGate _approval;
    private readonly AI.Agent.Application.IEventBus _bus;

    public RunHub(IApprovalGate approval, AI.Agent.Application.IEventBus bus)
    {
        _approval = approval; _bus = bus;
    }

    // Back-compat simple join method
    public Task Join(string runId) => Groups.AddToGroupAsync(Context.ConnectionId, runId);

    // Frontend expects these methods
    public Task JoinRunGroup(string runId) => Groups.AddToGroupAsync(Context.ConnectionId, runId);
    public Task LeaveRunGroup(string runId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, runId);

    public async Task Grant(string runId, string stepId)
    {
        var id = Guid.Parse(runId);
        _approval.Grant(id, stepId);
        await _bus.PublishAsync(new PermissionGranted(id, stepId));
    }

    public async Task Deny(string runId, string stepId, string reason)
    {
        var id = Guid.Parse(runId);
        _approval.Deny(id, stepId, reason);
        await _bus.PublishAsync(new PermissionDenied(id, stepId, reason));
    }
}
