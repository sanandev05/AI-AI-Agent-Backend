using System.Threading.Tasks;
using System.Collections.Generic;

namespace Domain.Agents
{
    /// <summary>
    /// Represents a modular tool/plugin that the agent can use to perform actions.
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// The unique name of the tool.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Executes the tool with the given input and returns the result.
        /// </summary>
        /// <param name="input">Input parameters or command for the tool.</param>
        /// <returns>Result of the tool execution.</returns>
        Task<string> ExecuteAsync(string input);
    }
}