namespace AI_AI_Agent.Domain.Agents
{
    public interface IAgent
    {
        /// <summary>
        /// Accepts a high-level user goal and initiates autonomous planning and execution.
        /// </summary>
        /// <param name="goal">A high-level description of the user's objective.</param>
        /// <returns>A task representing the asynchronous operation, with the final result or report.</returns>
        Task<string> AchieveGoalAsync(string goal);

        /// <summary>
        /// Returns a list of available tools/plugins the agent can use.
        /// </summary>
        IReadOnlyList<string> ListAvailableTools();

        /// <summary>
        /// Loads agent state from persistent memory.
        /// </summary>
        Task LoadStateAsync();

        /// <summary>
        /// Saves agent state to persistent memory.
        /// </summary>
        Task SaveStateAsync();
    }
}
