using AI_AI_Agent.Application.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AI_AI_Agent.API.Controllers
{
    [Route("api/manus-agent")]
    [ApiController]
    public class ManusAgentTestController : ControllerBase
    {
        private readonly ManusAgent _manusAgent;

        public ManusAgentTestController(ManusAgent manusAgent)
        {
            _manusAgent = manusAgent;
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestManusAgent([FromBody] ManusAgentTestRequest request)
        {
            var plan = await _manusAgent.AchieveGoalAsync(request.Goal);
            return Ok(new { plan });
        }
    }

    public class ManusAgentTestRequest
    {
        public string Goal { get; set; } = string.Empty;
    }
}
