using AI_AI_Agent.Domain.Tools;
using AI_AI_Agent.Domain.Agents;
using Microsoft.Extensions.Logging;

namespace AI_AI_Agent.Infrastructure.Services.Tools
{
    /// <summary>
    /// Context-aware tool selection service with agent-specific routing
    /// </summary>
    public class ToolSelectionService
    {
        private readonly ILogger<ToolSelectionService> _logger;
        private readonly Dictionary<string, ToolMetadata> _toolRegistry = new();
        private readonly Dictionary<AgentType, List<string>> _agentToolAllowList = new();

        public ToolSelectionService(ILogger<ToolSelectionService> logger)
        {
            _logger = logger;
            InitializeToolRegistry();
            InitializeAgentAllowLists();
        }

        #region Tool Registration

        /// <summary>
        /// Register a tool with its metadata
        /// </summary>
        public void RegisterTool(string toolName, ToolMetadata metadata)
        {
            _toolRegistry[toolName] = metadata;
            _logger.LogInformation("Registered tool {ToolName} with categories: {Categories}", 
                toolName, string.Join(", ", metadata.Categories));
        }

        /// <summary>
        /// Get all registered tools
        /// </summary>
        public List<ToolMetadata> GetAllTools()
        {
            return _toolRegistry.Values.ToList();
        }

        /// <summary>
        /// Get tool metadata by name
        /// </summary>
        public ToolMetadata? GetToolMetadata(string toolName)
        {
            _toolRegistry.TryGetValue(toolName, out var metadata);
            return metadata;
        }

        #endregion

        #region Tool Selection

        /// <summary>
        /// Select best tools for a given task based on context
        /// </summary>
        public List<ToolSelectionResult> SelectTools(ToolSelectionCriteria criteria)
        {
            _logger.LogInformation("Selecting tools for task: {Task}", criteria.TaskDescription);

            var candidateTools = GetCandidateTools(criteria);
            var scoredTools = ScoreTools(candidateTools, criteria);
            var selectedTools = RankAndFilterTools(scoredTools, criteria);

            _logger.LogInformation("Selected {Count} tools: {Tools}", 
                selectedTools.Count, string.Join(", ", selectedTools.Select(t => t.ToolName)));

            return selectedTools;
        }

        /// <summary>
        /// Select single best tool for a task
        /// </summary>
        public ToolSelectionResult? SelectBestTool(ToolSelectionCriteria criteria)
        {
            var tools = SelectTools(criteria);
            return tools.FirstOrDefault();
        }

        /// <summary>
        /// Check if agent is allowed to use a specific tool
        /// </summary>
        public bool IsToolAllowedForAgent(string toolName, AgentType agentType)
        {
            if (!_agentToolAllowList.ContainsKey(agentType))
                return true; // No restrictions

            return _agentToolAllowList[agentType].Contains(toolName);
        }

        private List<string> GetCandidateTools(ToolSelectionCriteria criteria)
        {
            var candidates = new List<string>();

            // Start with all tools
            foreach (var toolName in _toolRegistry.Keys)
            {
                // Check exclusions
                if (criteria.ExcludedTools.Contains(toolName))
                    continue;

                // Check agent allow-list
                if (!string.IsNullOrEmpty(criteria.AgentType))
                {
                    if (!Enum.TryParse<AgentType>(criteria.AgentType, out var agentType))
                        continue;

                    if (!IsToolAllowedForAgent(toolName, agentType))
                        continue;
                }

                var metadata = _toolRegistry[toolName];

                // Check expensive tool restriction
                if (metadata.IsExpensive && !criteria.AllowExpensiveTools)
                    continue;

                // Check required categories
                if (criteria.RequiredCategories.Any())
                {
                    if (!criteria.RequiredCategories.Any(cat => 
                        metadata.Categories.Contains(cat, StringComparer.OrdinalIgnoreCase)))
                        continue;
                }

                candidates.Add(toolName);
            }

            return candidates;
        }

        private List<ToolSelectionResult> ScoreTools(List<string> candidates, ToolSelectionCriteria criteria)
        {
            var results = new List<ToolSelectionResult>();

            foreach (var toolName in candidates)
            {
                var metadata = _toolRegistry[toolName];
                var score = CalculateToolScore(toolName, metadata, criteria);
                var reasoning = GenerateSelectionReasoning(toolName, metadata, criteria, score);

                var result = new ToolSelectionResult
                {
                    ToolName = toolName,
                    ConfidenceScore = score,
                    Reasoning = reasoning,
                    Metadata = metadata
                };

                // Add warnings
                if (metadata.RequiresApproval)
                    result.Warnings.Add("Tool requires approval before execution");
                if (metadata.IsExpensive)
                    result.Warnings.Add("Tool has high cost or resource usage");

                results.Add(result);
            }

            return results;
        }

        private double CalculateToolScore(string toolName, ToolMetadata metadata, ToolSelectionCriteria criteria)
        {
            double score = 0.0;
            int factors = 0;

            // Preferred tools get highest boost
            if (criteria.PreferredTools.Contains(toolName))
            {
                score += 0.4;
                factors++;
            }

            // Category match
            if (criteria.RequiredCategories.Any())
            {
                var matchingCategories = metadata.Categories
                    .Intersect(criteria.RequiredCategories, StringComparer.OrdinalIgnoreCase)
                    .Count();
                
                score += (matchingCategories / (double)criteria.RequiredCategories.Count) * 0.3;
                factors++;
            }

            // Task description keyword matching (simple implementation)
            var taskLower = criteria.TaskDescription.ToLower();
            var toolNameLower = toolName.ToLower();
            var descriptionLower = metadata.Description.ToLower();

            if (taskLower.Contains(toolNameLower) || descriptionLower.Split(' ').Any(word => taskLower.Contains(word)))
            {
                score += 0.2;
                factors++;
            }

            // Priority factor
            score += (metadata.Priority / 10.0) * 0.1;
            factors++;

            // Normalize score to 0-1 range
            return Math.Min(1.0, score);
        }

        private string GenerateSelectionReasoning(string toolName, ToolMetadata metadata, 
            ToolSelectionCriteria criteria, double score)
        {
            var reasons = new List<string>();

            if (criteria.PreferredTools.Contains(toolName))
                reasons.Add("preferred tool");

            if (criteria.RequiredCategories.Any())
            {
                var matchingCategories = metadata.Categories
                    .Intersect(criteria.RequiredCategories, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                
                if (matchingCategories.Any())
                    reasons.Add($"matches categories: {string.Join(", ", matchingCategories)}");
            }

            if (metadata.Priority >= 7)
                reasons.Add("high priority");

            if (metadata.AverageDuration < 1.0)
                reasons.Add("fast execution");

            return reasons.Any() 
                ? $"Selected due to: {string.Join(", ", reasons)} (confidence: {score:F2})"
                : $"Selected with confidence: {score:F2}";
        }

        private List<ToolSelectionResult> RankAndFilterTools(List<ToolSelectionResult> tools, 
            ToolSelectionCriteria criteria)
        {
            return tools
                .OrderByDescending(t => t.ConfidenceScore)
                .ThenByDescending(t => t.Metadata.Priority)
                .ThenBy(t => t.Metadata.AverageDuration)
                .Take(criteria.MaxTools)
                .ToList();
        }

        #endregion

        #region Agent Allow-Lists

        /// <summary>
        /// Configure which tools an agent type can use
        /// </summary>
        public void SetAgentToolAllowList(AgentType agentType, List<string> allowedTools)
        {
            _agentToolAllowList[agentType] = allowedTools;
            _logger.LogInformation("Set tool allow-list for {AgentType}: {Tools}", 
                agentType, string.Join(", ", allowedTools));
        }

        private void InitializeAgentAllowLists()
        {
            // Research Agent: search, browse, extract
            _agentToolAllowList[AgentType.Research] = new List<string>
            {
                "WebSearchTool", "WebBrowserTool", "ExtractorTool", "PdfReadTool",
                "TranslateTool", "ProductCompareTool"
            };

            // Code Agent: file operations, code execution
            _agentToolAllowList[AgentType.Code] = new List<string>
            {
                "FileWriterTool", "StepLoggerTool", "CalculatorTool"
            };

            // Files Agent: document operations
            _agentToolAllowList[AgentType.Files] = new List<string>
            {
                "PdfReadTool", "PdfCreateTool", "DocxReadTool", "DocxCreateTool",
                "ExcelReadTool", "PptxCreateTool", "FileWriterTool", "ExtractorTool"
            };

            // DataAnalysis Agent: data and charts
            _agentToolAllowList[AgentType.DataAnalysis] = new List<string>
            {
                "DataAnalyzeTool", "CsvAnalyzeTool", "ChartCreateTool", 
                "CalculatorTool", "ExcelReadTool", "FinanceRevenueTool"
            };
        }

        #endregion

        #region Tool Registry Initialization

        private void InitializeToolRegistry()
        {
            // Web & Search Tools
            RegisterTool("WebSearchTool", new ToolMetadata
            {
                Name = "WebSearchTool",
                Description = "Search the web using Google",
                Categories = new() { "web", "search", "research" },
                Tags = new() { "google", "search", "internet" },
                Priority = 9,
                AverageDuration = 2.0,
                IsExpensive = false
            });

            RegisterTool("WebBrowserTool", new ToolMetadata
            {
                Name = "WebBrowserTool",
                Description = "Navigate and interact with web pages",
                Categories = new() { "web", "browser", "research" },
                Tags = new() { "browser", "scraping", "navigation" },
                Priority = 8,
                AverageDuration = 3.0,
                IsExpensive = false
            });

            RegisterTool("ExtractorTool", new ToolMetadata
            {
                Name = "ExtractorTool",
                Description = "Extract structured data from web pages",
                Categories = new() { "web", "extraction", "research" },
                Tags = new() { "scraping", "extraction", "parsing" },
                Priority = 7,
                AverageDuration = 2.5,
                IsExpensive = false
            });

            // Document Tools
            RegisterTool("PdfReadTool", new ToolMetadata
            {
                Name = "PdfReadTool",
                Description = "Read and extract text from PDF files",
                Categories = new() { "document", "pdf", "files" },
                Tags = new() { "pdf", "reading", "document" },
                Priority = 8,
                AverageDuration = 1.5,
                IsExpensive = false
            });

            RegisterTool("PdfCreateTool", new ToolMetadata
            {
                Name = "PdfCreateTool",
                Description = "Create PDF documents",
                Categories = new() { "document", "pdf", "files", "creation" },
                Tags = new() { "pdf", "creation", "document" },
                Priority = 7,
                AverageDuration = 2.0,
                IsExpensive = false
            });

            RegisterTool("DocxReadTool", new ToolMetadata
            {
                Name = "DocxReadTool",
                Description = "Read Word documents",
                Categories = new() { "document", "word", "files" },
                Tags = new() { "docx", "word", "reading" },
                Priority = 7,
                AverageDuration = 1.0,
                IsExpensive = false
            });

            RegisterTool("DocxCreateTool", new ToolMetadata
            {
                Name = "DocxCreateTool",
                Description = "Create Word documents",
                Categories = new() { "document", "word", "files", "creation" },
                Tags = new() { "docx", "word", "creation" },
                Priority = 7,
                AverageDuration = 1.5,
                IsExpensive = false
            });

            RegisterTool("ExcelReadTool", new ToolMetadata
            {
                Name = "ExcelReadTool",
                Description = "Read Excel spreadsheets",
                Categories = new() { "document", "excel", "files", "data" },
                Tags = new() { "excel", "spreadsheet", "data" },
                Priority = 8,
                AverageDuration = 1.5,
                IsExpensive = false
            });

            RegisterTool("PptxCreateTool", new ToolMetadata
            {
                Name = "PptxCreateTool",
                Description = "Create PowerPoint presentations",
                Categories = new() { "document", "presentation", "files", "creation" },
                Tags = new() { "powerpoint", "presentation", "slides" },
                Priority = 6,
                AverageDuration = 2.5,
                IsExpensive = false
            });

            // Data & Analysis Tools
            RegisterTool("DataAnalyzeTool", new ToolMetadata
            {
                Name = "DataAnalyzeTool",
                Description = "Analyze data and generate insights",
                Categories = new() { "data", "analysis", "statistics" },
                Tags = new() { "analysis", "data", "statistics" },
                Priority = 9,
                AverageDuration = 3.0,
                IsExpensive = false
            });

            RegisterTool("CsvAnalyzeTool", new ToolMetadata
            {
                Name = "CsvAnalyzeTool",
                Description = "Analyze CSV files",
                Categories = new() { "data", "analysis", "csv" },
                Tags = new() { "csv", "data", "analysis" },
                Priority = 8,
                AverageDuration = 2.0,
                IsExpensive = false
            });

            RegisterTool("ChartCreateTool", new ToolMetadata
            {
                Name = "ChartCreateTool",
                Description = "Create charts and visualizations",
                Categories = new() { "visualization", "chart", "data" },
                Tags = new() { "chart", "visualization", "graph" },
                Priority = 8,
                AverageDuration = 2.0,
                IsExpensive = false
            });

            RegisterTool("CalculatorTool", new ToolMetadata
            {
                Name = "CalculatorTool",
                Description = "Perform mathematical calculations",
                Categories = new() { "math", "calculation", "utility" },
                Tags = new() { "math", "calculator", "computation" },
                Priority = 9,
                AverageDuration = 0.5,
                IsExpensive = false
            });

            // Utility Tools
            RegisterTool("FileWriterTool", new ToolMetadata
            {
                Name = "FileWriterTool",
                Description = "Write content to files",
                Categories = new() { "files", "writing", "utility" },
                Tags = new() { "file", "write", "save" },
                Priority = 8,
                AverageDuration = 0.5,
                IsExpensive = false
            });

            RegisterTool("StepLoggerTool", new ToolMetadata
            {
                Name = "StepLoggerTool",
                Description = "Log execution steps",
                Categories = new() { "logging", "utility", "debugging" },
                Tags = new() { "logging", "steps", "debugging" },
                Priority = 6,
                AverageDuration = 0.1,
                IsExpensive = false
            });

            RegisterTool("TranslateTool", new ToolMetadata
            {
                Name = "TranslateTool",
                Description = "Translate text between languages",
                Categories = new() { "translation", "language", "utility" },
                Tags = new() { "translation", "language", "localization" },
                Priority = 7,
                AverageDuration = 1.5,
                IsExpensive = true
            });

            // Specialized Tools
            RegisterTool("ProductCompareTool", new ToolMetadata
            {
                Name = "ProductCompareTool",
                Description = "Compare products and features",
                Categories = new() { "comparison", "research", "analysis" },
                Tags = new() { "comparison", "products", "features" },
                Priority = 7,
                AverageDuration = 2.5,
                IsExpensive = false
            });

            RegisterTool("FinanceRevenueTool", new ToolMetadata
            {
                Name = "FinanceRevenueTool",
                Description = "Analyze financial and revenue data",
                Categories = new() { "finance", "analysis", "data" },
                Tags = new() { "finance", "revenue", "analysis" },
                Priority = 7,
                AverageDuration = 2.0,
                IsExpensive = false
            });

            RegisterTool("CalendarTools", new ToolMetadata
            {
                Name = "CalendarTools",
                Description = "Manage calendar and scheduling",
                Categories = new() { "calendar", "scheduling", "utility" },
                Tags = new() { "calendar", "schedule", "events" },
                Priority = 6,
                AverageDuration = 1.0,
                IsExpensive = false
            });

            RegisterTool("EmailTools", new ToolMetadata
            {
                Name = "EmailTools",
                Description = "Send and manage emails",
                Categories = new() { "email", "communication", "utility" },
                Tags = new() { "email", "communication", "messaging" },
                Priority = 6,
                AverageDuration = 1.5,
                IsExpensive = false,
                RequiresApproval = true
            });
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Get tool selection statistics
        /// </summary>
        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["TotalTools"] = _toolRegistry.Count,
                ["ToolsByCategory"] = _toolRegistry.Values
                    .SelectMany(t => t.Categories)
                    .GroupBy(c => c)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ["ExpensiveTools"] = _toolRegistry.Values.Count(t => t.IsExpensive),
                ["ApprovalRequiredTools"] = _toolRegistry.Values.Count(t => t.RequiresApproval),
                ["AgentAllowLists"] = _agentToolAllowList.Count
            };
        }

        #endregion
    }
}
