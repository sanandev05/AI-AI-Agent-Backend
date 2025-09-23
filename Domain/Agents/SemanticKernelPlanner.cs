using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Domain.Agents
{
    /// <summary>
    /// Planner using Microsoft Semantic Kernel for plan generation and adaptation.
    /// </summary>
    public class SemanticKernelPlanner : IPlanner
    {
        private readonly Kernel _kernel;

        public SemanticKernelPlanner(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<string> GeneratePlanAsync(string goal)
        {
            // For prototype: use a prompt to generate a plan (tool:input)
            var prompt = $"Given the goal: '{goal}', suggest a tool and input in the format 'WebSearch:query'.";
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString();
        }

        public async Task<string> AdaptPlanAsync(string currentPlan, string feedback)
        {
            // For prototype: use a prompt to adapt the plan
            var prompt = $"The plan '{currentPlan}' failed with error: '{feedback}'. Suggest an alternative tool and input in the format 'WebSearch:query'.";
            var result = await _kernel.InvokePromptAsync(prompt);
            return result.ToString();
        }
    }
}