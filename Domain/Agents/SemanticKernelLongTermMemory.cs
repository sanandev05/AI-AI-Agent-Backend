using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Domain.Agents
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
            await _semanticMemory.SaveInformationAsync(collection: "longterm", text: value, id: key);
        }

        public async Task<string?> RecallAsync(string key)
        {
            var mem = await _semanticMemory.GetAsync(collection: "longterm", key);
            return mem?.Metadata.Text;
        }
    }
}