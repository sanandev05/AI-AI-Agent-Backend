using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Assistants;
using AI_AI_Agent.Domain.Agents;
using DomainAgentMetadata = AI_AI_Agent.Domain.Agents.AgentMetadata;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only
#pragma warning disable OPENAI001 // Type is for evaluation purposes only

namespace AI_AI_Agent.Infrastructure.Services.Agents
{
    /// <summary>
    /// Orchestrator Agent that coordinates task planning, agent routing, and result synthesis
    /// </summary>
    public class OrchestratorAgent
    {
        private readonly AssistantAgentService _assistantService;
        private readonly IAgentRegistry _agentRegistry;
        private OpenAIAssistantAgent? _orchestrator;
        private const string OrchestratorId = "orchestrator-main";

        public OrchestratorAgent(
            AssistantAgentService assistantService,
            IAgentRegistry agentRegistry)
        {
            _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
            _agentRegistry = agentRegistry ?? throw new ArgumentNullException(nameof(agentRegistry));
        }

        /// <summary>
        /// Initialize the orchestrator agent
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            var metadata = new DomainAgentMetadata
            {
                Id = OrchestratorId,
                Name = "Orchestrator",
                Description = "Main coordinator agent that plans tasks and delegates to specialized agents",
                Type = AgentType.Orchestrator,
                Instructions = GetOrchestratorInstructions(),
                Capabilities = new List<AgentCapability>
                {
                    new AgentCapability
                    {
                        Name = "TaskPlanning",
                        Description = "Decompose complex tasks into subtasks and create execution plans"
                    },
                    new AgentCapability
                    {
                        Name = "AgentRouting",
                        Description = "Determine which specialized agent to use for each subtask"
                    },
                    new AgentCapability
                    {
                        Name = "ResultSynthesis",
                        Description = "Aggregate results from multiple agents into coherent responses"
                    },
                    new AgentCapability
                    {
                        Name = "ErrorRecovery",
                        Description = "Handle failures and implement retry/fallback strategies"
                    }
                },
                Configuration = new Dictionary<string, string>
                {
                    { "ModelId", "gpt-4o" },
                    { "Temperature", "0.7" }
                }
            };

            _orchestrator = await _assistantService.CreateAssistantAsync(metadata, cancellationToken);
        }

        /// <summary>
        /// Process a user request by planning and coordinating specialized agents
        /// </summary>
        public async Task<string> ProcessRequestAsync(
            string userRequest,
            OpenAIAssistantAgentThread thread,
            CancellationToken cancellationToken = default)
        {
            if (_orchestrator == null)
            {
                throw new InvalidOperationException("Orchestrator not initialized. Call InitializeAsync first.");
            }

            // Step 1: Analyze the request and create a plan
            var planningPrompt = $@"
User Request: {userRequest}

Available Specialized Agents:
- ResearchAgent: Web search, information gathering, summarization, citation extraction
- CodeAgent: Code generation, execution, review, and debugging
- FilesAgent: Document processing, PDF reading, file manipulation
- DataAnalysisAgent: Data visualization, statistical analysis, chart creation

Your Task:
1. Analyze the user's request
2. Determine which specialized agents are needed
3. Create a step-by-step execution plan
4. For each step, specify which agent to use and what to ask them

Respond with a clear plan in this format:
PLAN:
1. [Agent Name]: [Task description]
2. [Agent Name]: [Task description]
...

Then I will coordinate the execution and synthesize the results.
";

            // Get the orchestrator's plan
            var planResponse = await _assistantService.SendMessageAsync(
                OrchestratorId,
                thread,
                planningPrompt,
                cancellationToken
            );

            // For now, return the plan (in later phases, we'll actually execute it)
            return $"**Orchestrator Plan:**\n\n{planResponse}";
        }

        /// <summary>
        /// Stream responses from the orchestrator
        /// </summary>
        public async IAsyncEnumerable<string> ProcessRequestStreamingAsync(
            string userRequest,
            OpenAIAssistantAgentThread thread,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_orchestrator == null)
            {
                throw new InvalidOperationException("Orchestrator not initialized. Call InitializeAsync first.");
            }

            var planningPrompt = $@"
User Request: {userRequest}

Available Specialized Agents:
- ResearchAgent: Web search, information gathering, summarization
- CodeAgent: Code generation, execution, review, debugging
- FilesAgent: Document processing, PDF reading, file manipulation
- DataAnalysisAgent: Data visualization, statistical analysis, charts

Analyze the request and create an execution plan, then coordinate the specialized agents to complete the task.
";

            await foreach (var chunk in _assistantService.InvokeStreamingAsync(
                OrchestratorId,
                thread,
                planningPrompt,
                cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    yield return chunk.Content;
                }
            }
        }

        /// <summary>
        /// Get available specialized agents
        /// </summary>
        public async Task<IReadOnlyList<DomainAgentMetadata>> GetAvailableAgentsAsync(
            CancellationToken cancellationToken = default)
        {
            return await _agentRegistry.GetAllAgentsAsync(cancellationToken);
        }

        private string GetOrchestratorInstructions()
        {
            return @"
You are the Orchestrator Agent, the main coordinator of an autonomous multi-agent AI system. Your role is to:

**Core Responsibilities:**
1. **Task Analysis**: Break down complex user requests into manageable subtasks
2. **Agent Routing**: Determine which specialized agent is best suited for each subtask
3. **Coordination**: Manage the execution flow and handle dependencies between tasks
4. **Result Synthesis**: Combine outputs from multiple agents into coherent, comprehensive responses
5. **Error Handling**: Detect failures and implement retry or alternative strategies

**Available Specialized Agents:**
- **ResearchAgent**: Expert in web search, information gathering, summarization, fact-checking, and citation
- **CodeAgent**: Specializes in code generation, execution, debugging, review, and multi-language support
- **FilesAgent**: Handles document processing, PDF reading, file organization, and content extraction
- **DataAnalysisAgent**: Performs data analysis, creates visualizations, generates charts, and statistical analysis

**Decision-Making Guidelines:**
- For research/information tasks → Use ResearchAgent
- For programming/code tasks → Use CodeAgent
- For document/file tasks → Use FilesAgent
- For data/analytics tasks → Use DataAnalysisAgent
- For complex tasks → Use multiple agents in sequence or parallel

**Communication Style:**
- Be clear, concise, and professional
- Explain your reasoning and plan
- Provide status updates during execution
- Highlight important findings
- Ask for clarification when needed

**Safety & Best Practices:**
- Validate all inputs before delegating
- Ensure safe code execution practices
- Respect file access permissions
- Maintain user privacy and data security
- Log all decisions for transparency

Your goal is to provide the most efficient and accurate solution by intelligently coordinating specialized agents.
";
        }
    }
}
