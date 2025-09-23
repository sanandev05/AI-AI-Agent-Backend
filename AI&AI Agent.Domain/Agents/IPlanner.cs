using System.Threading.Tasks;

namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Defines the contract for a planner that generates and adapts plans for the agent.
    /// </summary>
    public interface IPlanner
    {
        /// <summary>
        /// Generates a plan to achieve the specified goal.
        /// </summary>
        /// <param name="goal">The high-level goal to achieve.</param>
        /// <returns>A plan as a string or structured object.</returns>
        Task<string> GeneratePlanAsync(string goal);

        /// <summary>
        /// Adapts the current plan based on feedback or failure.
        /// </summary>
        /// <param name="originalGoal">The original high-level goal.</param>
        /// <param name="currentPlan">The current plan that failed.</param>
        /// <param name="feedback">Feedback or error information.</param>
        /// <returns>The adapted plan.</returns>
        Task<string> AdaptPlanAsync(string originalGoal, string currentPlan, string feedback);
    }
}
