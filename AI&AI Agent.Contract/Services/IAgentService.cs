using AI_AI_Agent.Contract.DTOs;
using System.Threading.Tasks;

namespace AI_AI_Agent.Contract.Services
{
    public interface IAgentService
    {
        Task<string> ExecuteGoalAsync(AgentRequestDto request);
    }
}
