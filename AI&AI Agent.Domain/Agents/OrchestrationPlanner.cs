using Microsoft.SemanticKernel;
using System.Threading.Tasks;

namespace AI_AI_Agent.Domain.Agents
{
    public class OrchestrationPlanner : IPlanner
    {
        private readonly Kernel _kernel;
        private const string PlannerPrompt = @"
You are a planner for an AI agent. Your task is to create a plan for the agent to achieve a given goal.
The agent has a set of tools available. For a given goal, you must create a plan that consists of a sequence of tool calls.
The plan should be in the format: ToolName.FunctionName(arg1, arg2, ...).

Here are the available tools:
- Playwright:
  - GoToAsync(url): Navigates to a specific URL.
  - ClickAsync(selector): Clicks a UI element on the page specified by a selector.
  - TypeAsync(selector, text): Types text into an input field on the page specified by a selector.
  - ReadTextAsync(selector): Reads the text content of a UI element on the page specified by a selector.
  - ScreenshotAsync(filePath): Takes a screenshot of the current page and saves it to a file.
- WebSearch:
  - SearchAsync(query): Searches the web for the given query.

Your plan should be a single line of text.

Goal: {{$input}}
Plan:
";

        public OrchestrationPlanner(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<string> GeneratePlanAsync(string goal)
        {
            var function = _kernel.CreateFunctionFromPrompt(PlannerPrompt);
            var result = await _kernel.InvokeAsync(function, new() { { "input", goal } });
            return result.GetValue<string>().Trim();
        }

        public async Task<string> AdaptPlanAsync(string originalGoal, string currentPlan, string feedback)
        {
            var adaptationPrompt = @$"
You are a planner for an AI agent. The agent failed to execute the following plan:
{currentPlan}

The error was:
{feedback}

Please generate a new plan to achieve the original goal. The new plan must be a single line of text in the format: ToolName.FunctionName(arg1, arg2, ...).

Original Goal: {{$goal}}
New Plan:
";
            var function = _kernel.CreateFunctionFromPrompt(adaptationPrompt);
            var result = await _kernel.InvokeAsync(function, new() { { "goal", originalGoal } });
            return result.GetValue<string>().Trim();
        }
    }
}