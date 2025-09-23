using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Domain.Agents
{
    /// <summary>
    /// Simple in-memory short-term memory for the agent.
    /// </summary>
    public class ShortTermMemory : IMemory
    {
        private readonly ConcurrentDictionary<string, string> _memory = new();

        public Task StoreAsync(string key, string value)
        {
            _memory[key] = value;
            return Task.CompletedTask;
        }

        public Task<string?> RecallAsync(string key)
        {
            _memory.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }
    }
}