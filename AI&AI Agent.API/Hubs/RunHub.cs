using Microsoft.AspNetCore.SignalR;
using AI_AI_Agent.Application;

namespace AI_AI_Agent.API.Hubs;

public sealed class RunHub : Hub
{
    private readonly IApprovalCoordinator _approvals;
    public RunHub(IApprovalCoordinator approvals)
    {
        _approvals = approvals;
    }
    public Task Join(string runId) => Groups.AddToGroupAsync(Context.ConnectionId, runId);

    public Task Grant(string runId, string stepId)
    {
        if (!Guid.TryParse(runId, out var rid)) throw new ArgumentException("runId must be a GUID", nameof(runId));
        return _approvals.GrantAsync(rid, stepId);
    }

    public Task Deny(string runId, string stepId, string reason)
    {
        if (!Guid.TryParse(runId, out var rid)) throw new ArgumentException("runId must be a GUID", nameof(runId));
        return _approvals.DenyAsync(rid, stepId, reason);
    }
}
