using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AI_AI_Agent.Domain.Agents;
using AI_AI_Agent.Application.Agent;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AI_AI_Agent.API.Controllers
{
    [Authorize]
    [Route("api/agent")]
    [ApiController]
    public class AgentController : ControllerBase
    {
        private readonly IOrchestrator _orchestrator;
        private readonly AI_AI_Agent.Application.Services.IRunCancellationRegistry _runs;

        public AgentController(IOrchestrator orchestrator, AI_AI_Agent.Application.Services.IRunCancellationRegistry runs)
        {
            _orchestrator = orchestrator;
            _runs = runs;
        }

        [HttpPost("chat/{chatId:guid}")]
    public IActionResult ExecuteAgentTurn(Guid chatId, [FromBody] AgentTurnRequest request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Create a dedicated cancellation token for this run and start the loop.
            var token = _runs.Register(chatId.ToString());
            _ = _orchestrator.RunAsync(chatId.ToString(), request.Prompt, token)
                .ContinueWith(_ => _runs.Complete(chatId.ToString()));

            return Accepted(new { message = "Agent loop started. Listen for events on the SignalR hub.", chatId });
        }

        [HttpPost("chat/{chatId:guid}/cancel")]
        public IActionResult CancelAgentRun(Guid chatId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var ok = _runs.Cancel(chatId.ToString());
            if (!ok) return NotFound(new { message = "No active run found for this chat." });
            return Ok(new { message = "Cancellation requested." });
        }
    }

    public class AgentTurnRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }
}