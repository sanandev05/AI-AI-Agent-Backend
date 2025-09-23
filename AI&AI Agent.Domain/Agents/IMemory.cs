using System.Threading.Tasks;

namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Defines the contract for agent memory (short-term and long-term).
    /// </summary>
    public interface IMemory
    {
        /// <summary>
        /// Stores information in memory.
        /// </summary>
        /// <param name="key">The key or context for the memory.</param>
        /// <param name="value">The information to store.</param>
        Task StoreAsync(string key, string value);

        /// <summary>
        /// Retrieves information from memory.
        /// </summary>
        /// <param name="key">The key or context for the memory.</param>
        /// <returns>The stored information, or null if not found.</returns>
        Task<string?> RecallAsync(string key);
    }
}
