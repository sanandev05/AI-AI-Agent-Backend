using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AI_AI_Agent.Contract.Services;
using AI_AI_Agent.Domain.Agents;

namespace AI_AI_Agent.API.Controllers
{
    //[Authorize]
    [Route("api/agent")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly IAgent _agent;

        public AgentController(IAgent agent)
        {
            _agent = agent;
        }

        /// <summary>
        /// Accepts a high-level goal and returns the agent's autonomous result.
        /// </summary>
        /// <param name="request">The goal request.</param>
        /// <returns>The agent's result or error.</returns>
        [HttpPost("achieve-goal")]
        public async Task<IActionResult> AchieveGoal([FromBody] AgentGoalRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Goal))
                return BadRequest("Goal is required.");
            try
            {
                var result = await _agent.AchieveGoalAsync(request.Goal);
                return Ok(new { result });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request DTO for agent goal.
    /// </summary>
    public class AgentGoalRequest
    {
        public string? Goal { get; set; }
    }
}
