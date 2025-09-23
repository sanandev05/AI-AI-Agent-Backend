using Microsoft.SemanticKernel.Memory;
using System.Threading.Tasks;

namespace AI_AI_Agent.Domain.Agents
{
    /// <summary>
    /// Long-term memory using Microsoft Semantic Kernel.
    /// </summary>
    public class SemanticKernelLongTermMemory : IMemory
    {
        private readonly ISemanticTextMemory _semanticMemory;

        public SemanticKernelLongTermMemory(ISemanticTextMemory semanticMemory)
        {
            _semanticMemory = semanticMemory;
        }

        public async Task StoreAsync(string key, string value)
        {
            await _semanticMemory.SaveInformationAsync("longterm", text: value, id: key);
        }

        public async Task<string?> RecallAsync(string key)
        {
            var mem = await _semanticMemory.GetAsync("longterm", key);
            return mem?.Metadata.Text;
        }
    }
}
