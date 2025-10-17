using AI.Agent.Application;
using AI.Agent.Domain;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authorization;

namespace AI_AI_Agent.API.Controllers;

[ApiController]
[Route("api/runs")]
[Authorize]
public sealed class RunsController : ControllerBase
{
    private readonly IPlanner _planner;
    private readonly IExecutor _executor;

    public RunsController(IPlanner planner, IExecutor executor)
    {
        _planner = planner; _executor = executor;
    }

    public sealed record StartRunRequest(string Goal);
    public sealed record StartRunResponse(Guid RunId);

    [HttpPost]
    [ProducesResponseType(typeof(StartRunResponse), 202)]
    public async Task<IActionResult> StartRun([FromBody] StartRunRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Goal)) return BadRequest("goal is required");
        var runId = Guid.NewGuid();
        var plan = await _planner.MakePlanAsync(req.Goal, ct);
        _ = Task.Run(() => _executor.ExecuteAsync(runId, plan, CancellationToken.None));
        return Accepted(new StartRunResponse(runId));
    }
}
