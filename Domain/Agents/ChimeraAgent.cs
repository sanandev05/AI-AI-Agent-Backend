using AI_AI_Agent.Domain.Agents;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace AI_AI_Agent.Domain.Agents
{
    public class ChimeraAgent : IAgent
    {
        private readonly ILogger<ChimeraAgent> _logger;
        private readonly IPlanner _planner;
        private readonly ISearchTool _searchTool;

        public ChimeraAgent(
            IPlanner planner,
            ISearchTool searchTool,
            ILogger<ChimeraAgent> logger)
        {
            _planner = planner;
            _searchTool = searchTool;
            _logger = logger;
        }

        public async Task<string> ExecuteAsync(string goal)
        {
            _logger.LogInformation("Executing ChimeraAgent with goal: {Goal}", goal);

            var plan = await _planner.CreatePlanAsync(goal);
            _logger.LogInformation("Execution plan: {Plan}", plan);

            var result = await _searchTool.SearchAsync(goal);
            _logger.LogInformation("Search result: {Result}", result);

            return result;
        }
    }
}