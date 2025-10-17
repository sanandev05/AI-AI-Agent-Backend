using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.OpenAI;
using AI_AI_Agent.Domain.Agents;
using DomainAgentMetadata = AI_AI_Agent.Domain.Agents.AgentMetadata;

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only

namespace AI_AI_Agent.Infrastructure.Services.Agents
{
    /// <summary>
    /// Factory for creating and managing specialized agents
    /// </summary>
    public class SpecializedAgentFactory
    {
        private readonly AssistantAgentService _assistantService;
        private readonly Dictionary<string, OpenAIAssistantAgent> _specializedAgents = new();

        public SpecializedAgentFactory(AssistantAgentService assistantService)
        {
            _assistantService = assistantService ?? throw new ArgumentNullException(nameof(assistantService));
        }

        /// <summary>
        /// Initialize all specialized agents
        /// </summary>
        public async Task InitializeAllAgentsAsync(CancellationToken cancellationToken = default)
        {
            await Task.WhenAll(
                CreateResearchAgentAsync(cancellationToken),
                CreateCodeAgentAsync(cancellationToken),
                CreateFilesAgentAsync(cancellationToken),
                CreateDataAnalysisAgentAsync(cancellationToken)
            );
        }

        /// <summary>
        /// Create Research Agent - Expert in web search and information gathering
        /// </summary>
        public async Task<OpenAIAssistantAgent> CreateResearchAgentAsync(CancellationToken cancellationToken = default)
        {
            var metadata = new DomainAgentMetadata
            {
                Id = "research-agent",
                Name = "ResearchAgent",
                Description = "Expert in web search, information gathering, summarization, and citation",
                Type = AgentType.Research,
                Instructions = @"
You are the Research Agent, an expert in information gathering and analysis.

**Your Capabilities:**
- Web search and information retrieval
- Content summarization and synthesis
- Fact-checking and verification
- Citation and source tracking
- Multi-source information correlation

**Tools Available:**
- WebSearchTool: Search the internet for information
- WebBrowserTool: Browse and extract content from web pages
- Summarization capabilities

**Best Practices:**
- Always cite your sources with URLs
- Verify information from multiple sources when possible
- Distinguish between facts and opinions
- Provide publication dates when available
- Summarize complex information clearly
- Flag uncertain or conflicting information

**Response Format:**
- Start with a clear, concise answer
- Provide supporting details
- Include citations [Source: URL]
- Add relevant context
- Highlight key findings

Your goal is to provide accurate, well-researched, and properly cited information.",
                Capabilities = new List<AgentCapability>
                {
                    new AgentCapability { Name = "WebSearch", Description = "Search the internet for information" },
                    new AgentCapability { Name = "Summarization", Description = "Summarize and synthesize information" },
                    new AgentCapability { Name = "FactChecking", Description = "Verify and validate information" },
                    new AgentCapability { Name = "CitationExtraction", Description = "Extract and format citations" }
                },
                AvailableTools = new List<string> { "WebSearchTool", "WebBrowserTool" },
                Configuration = new Dictionary<string, string>
                {
                    { "ModelId", "gpt-4o" },
                    { "Temperature", "0.3" } // Lower temperature for factual accuracy
                }
            };

            var agent = await _assistantService.CreateAssistantAsync(metadata, cancellationToken);
            _specializedAgents[metadata.Id] = agent;
            return agent;
        }

        /// <summary>
        /// Create Code Agent - Expert in code generation and execution
        /// </summary>
        public async Task<OpenAIAssistantAgent> CreateCodeAgentAsync(CancellationToken cancellationToken = default)
        {
            var metadata = new DomainAgentMetadata
            {
                Id = "code-agent",
                Name = "CodeAgent",
                Description = "Expert in code generation, execution, debugging, and review",
                Type = AgentType.Code,
                Instructions = @"
You are the Code Agent, an expert software engineer and code specialist.

**Your Capabilities:**
- Code generation in multiple languages (Python, JavaScript, C#, etc.)
- Code execution and testing
- Debugging and error analysis
- Code review and optimization
- Security vulnerability detection
- Best practices and design patterns

**Tools Available:**
- Code execution environment (sandboxed)
- Package management and dependencies
- Code analysis tools

**Best Practices:**
- Write clean, readable, and maintainable code
- Include comments and documentation
- Follow language-specific conventions
- Handle errors gracefully
- Consider security implications
- Test code before delivering
- Explain complex logic clearly

**Safety Guidelines:**
- Never execute potentially harmful code
- Validate all inputs
- Use sandboxed environments
- Check package safety
- Respect resource limits
- Log all executions

**Response Format:**
```language
// Code with comments
code here
```
- Explanation of the code
- How to use it
- Potential issues or limitations
- Test results if executed

Your goal is to provide high-quality, safe, and well-documented code solutions.",
                Capabilities = new List<AgentCapability>
                {
                    new AgentCapability { Name = "CodeGeneration", Description = "Generate code in multiple languages" },
                    new AgentCapability { Name = "CodeExecution", Description = "Execute and test code safely" },
                    new AgentCapability { Name = "Debugging", Description = "Debug and fix code issues" },
                    new AgentCapability { Name = "CodeReview", Description = "Review and optimize code" }
                },
                AvailableTools = new List<string> { "CodeExecutionTool", "PackageManagerTool" },
                Configuration = new Dictionary<string, string>
                {
                    { "ModelId", "gpt-4o" },
                    { "Temperature", "0.2" }, // Low temperature for precise code
                    { "EnableCodeInterpreter", "true" }
                }
            };

            var agent = await _assistantService.CreateAssistantAsync(metadata, cancellationToken);
            _specializedAgents[metadata.Id] = agent;
            return agent;
        }

        /// <summary>
        /// Create Files Agent - Expert in document processing and file manipulation
        /// </summary>
        public async Task<OpenAIAssistantAgent> CreateFilesAgentAsync(CancellationToken cancellationToken = default)
        {
            var metadata = new DomainAgentMetadata
            {
                Id = "files-agent",
                Name = "FilesAgent",
                Description = "Expert in document processing, PDF reading, and file manipulation",
                Type = AgentType.Files,
                Instructions = @"
You are the Files Agent, an expert in document processing and file management.

**Your Capabilities:**
- PDF document reading and extraction
- Word document processing
- Excel spreadsheet analysis
- Text file manipulation
- File organization and management
- Content search and indexing
- Format conversion

**Tools Available:**
- PDFReaderTool: Extract text and data from PDFs
- WordProcessorTool: Handle Word documents
- ExcelProcessorTool: Process Excel files
- FileWriterTool: Create and modify files
- FileSearchTool: Search file contents

**Best Practices:**
- Validate file types and sizes
- Handle encoding properly
- Preserve formatting when important
- Extract structured data accurately
- Organize information logically
- Respect file permissions
- Maintain data integrity

**Safety Guidelines:**
- Check file sizes before processing
- Validate file types
- Scan for malicious content
- Respect access permissions
- Don't expose sensitive information
- Handle errors gracefully

**Response Format:**
- Summary of the document/file
- Extracted key information
- Structured data if applicable
- Relevant sections or highlights
- Metadata (author, date, etc.)

Your goal is to efficiently process and extract value from various document formats.",
                Capabilities = new List<AgentCapability>
                {
                    new AgentCapability { Name = "DocumentProcessing", Description = "Process various document formats" },
                    new AgentCapability { Name = "ContentExtraction", Description = "Extract text and data from files" },
                    new AgentCapability { Name = "FileManipulation", Description = "Create, modify, and organize files" },
                    new AgentCapability { Name = "FormatConversion", Description = "Convert between file formats" }
                },
                AvailableTools = new List<string> { "PDFReaderTool", "WordProcessorTool", "ExcelProcessorTool", "FileWriterTool" },
                Configuration = new Dictionary<string, string>
                {
                    { "ModelId", "gpt-4o" },
                    { "Temperature", "0.3" },
                    { "EnableFileSearch", "true" }
                }
            };

            var agent = await _assistantService.CreateAssistantAsync(metadata, cancellationToken);
            _specializedAgents[metadata.Id] = agent;
            return agent;
        }

        /// <summary>
        /// Create Data Analysis Agent - Expert in data analysis and visualization
        /// </summary>
        public async Task<OpenAIAssistantAgent> CreateDataAnalysisAgentAsync(CancellationToken cancellationToken = default)
        {
            var metadata = new DomainAgentMetadata
            {
                Id = "data-analysis-agent",
                Name = "DataAnalysisAgent",
                Description = "Expert in data analysis, visualization, and statistical analysis",
                Type = AgentType.DataAnalysis,
                Instructions = @"
You are the Data Analysis Agent, an expert in data science and statistical analysis.

**Your Capabilities:**
- Data cleaning and preprocessing
- Statistical analysis (descriptive, inferential)
- Data visualization and charting
- Trend analysis and forecasting
- Correlation and regression analysis
- Hypothesis testing
- Machine learning insights

**Tools Available:**
- ChartCreationTool: Generate various types of charts
- StatisticalAnalysisTool: Perform statistical calculations
- DataTransformationTool: Clean and transform data
- VisualizationTool: Create advanced visualizations

**Best Practices:**
- Clean and validate data first
- Choose appropriate visualizations
- Use proper statistical methods
- Explain findings clearly
- Highlight key insights
- Consider data limitations
- Provide actionable recommendations

**Analysis Workflow:**
1. Understand the data and question
2. Clean and preprocess data
3. Perform appropriate analysis
4. Create clear visualizations
5. Interpret and explain results
6. Provide insights and recommendations

**Response Format:**
- Executive summary of findings
- Statistical results with explanations
- Visualizations (charts, graphs)
- Key insights and patterns
- Recommendations or next steps
- Limitations and caveats

Your goal is to extract meaningful insights from data and present them clearly.",
                Capabilities = new List<AgentCapability>
                {
                    new AgentCapability { Name = "StatisticalAnalysis", Description = "Perform statistical analysis" },
                    new AgentCapability { Name = "DataVisualization", Description = "Create charts and visualizations" },
                    new AgentCapability { Name = "TrendAnalysis", Description = "Identify patterns and trends" },
                    new AgentCapability { Name = "DataCleaning", Description = "Clean and preprocess data" }
                },
                AvailableTools = new List<string> { "ChartCreationTool", "StatisticalAnalysisTool", "DataTransformationTool" },
                Configuration = new Dictionary<string, string>
                {
                    { "ModelId", "gpt-4o" },
                    { "Temperature", "0.2" }, // Low temperature for accurate analysis
                    { "EnableCodeInterpreter", "true" } // For data processing
                }
            };

            var agent = await _assistantService.CreateAssistantAsync(metadata, cancellationToken);
            _specializedAgents[metadata.Id] = agent;
            return agent;
        }

        /// <summary>
        /// Get a specialized agent by ID
        /// </summary>
        public OpenAIAssistantAgent? GetAgent(string agentId)
        {
            _specializedAgents.TryGetValue(agentId, out var agent);
            return agent;
        }

        /// <summary>
        /// Get all specialized agents
        /// </summary>
        public IReadOnlyDictionary<string, OpenAIAssistantAgent> GetAllAgents()
        {
            return _specializedAgents;
        }
    }
}
