using AI_AI_Agent.Domain.ProjectAnalysis;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace AI_AI_Agent.Infrastructure.Services.ProjectAnalysis
{
    /// <summary>
    /// Project understanding service for codebase analysis
    /// </summary>
    public class ProjectUnderstandingService
    {
        private readonly ILogger<ProjectUnderstandingService> _logger;
        private readonly ConcurrentDictionary<string, Domain.ProjectAnalysis.ProjectAnalysis> _analyses = new();

        public ProjectUnderstandingService(ILogger<ProjectUnderstandingService> logger)
        {
            _logger = logger;
        }

        #region Codebase Analysis

        public async Task<Domain.ProjectAnalysis.ProjectAnalysis> AnalyzeProjectAsync(string projectPath)
        {
            _logger.LogInformation("Starting analysis of project at {ProjectPath}", projectPath);

            var analysis = new Domain.ProjectAnalysis.ProjectAnalysis
            {
                ProjectPath = projectPath,
                ProjectName = Path.GetFileName(projectPath)
            };

            // Analyze project structure
            analysis.Structure = AnalyzeStructure(projectPath);

            // Analyze files
            analysis.Files = await AnalyzeFilesAsync(projectPath);

            // Detect languages and frameworks
            analysis.Languages = DetectLanguages(analysis.Files);
            analysis.Frameworks = DetectFrameworks(analysis.Files);

            // Build dependency graph
            analysis.Dependencies = BuildDependencyGraph(analysis.Files);

            // Generate insights
            analysis.Insights = GenerateInsights(analysis);

            // Calculate metrics
            analysis.Metrics = CalculateMetrics(analysis.Files);

            // Generate refactoring suggestions
            analysis.Suggestions = GenerateSuggestions(analysis);

            _analyses[analysis.Id] = analysis;

            _logger.LogInformation("Completed analysis of project {ProjectName}", analysis.ProjectName);
            return analysis;
        }

        private ProjectStructure AnalyzeStructure(string projectPath)
        {
            var structure = new ProjectStructure
            {
                RootPath = projectPath
            };

            if (!Directory.Exists(projectPath))
            {
                return structure;
            }

            var directories = Directory.GetDirectories(projectPath, "*", SearchOption.AllDirectories);

            foreach (var dir in directories)
            {
                var dirInfo = new DirectoryInfo(dir);
                var projectDir = new ProjectDirectory
                {
                    Path = dir,
                    Name = dirInfo.Name,
                    FileCount = Directory.GetFiles(dir).Length,
                    Type = ClassifyDirectory(dirInfo.Name)
                };
                structure.Directories.Add(projectDir);
            }

            structure.TotalFiles = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories).Length;
            structure.TotalSizeBytes = CalculateDirectorySize(projectPath);

            return structure;
        }

        private DirectoryType ClassifyDirectory(string dirName)
        {
            var lower = dirName.ToLower();
            if (lower.Contains("test")) return DirectoryType.Test;
            if (lower.Contains("src") || lower.Contains("source")) return DirectoryType.Source;
            if (lower.Contains("config") || lower.Contains("settings")) return DirectoryType.Configuration;
            if (lower.Contains("doc")) return DirectoryType.Documentation;
            if (lower.Contains("build") || lower.Contains("bin") || lower.Contains("obj")) return DirectoryType.Build;
            if (lower.Contains("resource") || lower.Contains("asset")) return DirectoryType.Resources;
            return DirectoryType.Other;
        }

        private async Task<List<CodeFile>> AnalyzeFilesAsync(string projectPath)
        {
            var files = new List<CodeFile>();
            var codeExtensions = new[] { ".cs", ".py", ".js", ".ts", ".java", ".cpp", ".c", ".h" };

            var codeFiles = Directory.GetFiles(projectPath, "*.*", SearchOption.AllDirectories)
                .Where(f => codeExtensions.Contains(Path.GetExtension(f).ToLower()))
                .Take(100); // Limit for performance

            foreach (var filePath in codeFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(filePath);
                    var codeFile = AnalyzeCodeFile(filePath, content);
                    files.Add(codeFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to analyze file {FilePath}", filePath);
                }
            }

            return files;
        }

        private CodeFile AnalyzeCodeFile(string filePath, string content)
        {
            var lines = content.Split('\n');
            var codeFile = new CodeFile
            {
                Path = filePath,
                Language = DetectLanguage(filePath),
                LineCount = lines.Length
            };

            // Count line types
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    codeFile.BlankLines++;
                else if (IsCommentLine(trimmed, codeFile.Language))
                    codeFile.CommentLines++;
                else
                    codeFile.CodeLines++;
            }

            // Extract symbols
            codeFile.Symbols = ExtractSymbols(content, codeFile.Language);

            // Calculate complexity
            codeFile.ComplexityScore = CalculateComplexity(content);

            return codeFile;
        }

        #endregion

        #region Language Detection

        private List<string> DetectLanguages(List<CodeFile> files)
        {
            return files.Select(f => f.Language).Distinct().OrderBy(l => l).ToList();
        }

        private List<string> DetectFrameworks(List<CodeFile> files)
        {
            var frameworks = new HashSet<string>();

            foreach (var file in files)
            {
                var content = file.Path;
                if (content.Contains("React")) frameworks.Add("React");
                if (content.Contains("Angular")) frameworks.Add("Angular");
                if (content.Contains("Vue")) frameworks.Add("Vue");
                if (content.Contains("ASP.NET")) frameworks.Add("ASP.NET Core");
                if (content.Contains("Django")) frameworks.Add("Django");
                if (content.Contains("Flask")) frameworks.Add("Flask");
                if (content.Contains("Express")) frameworks.Add("Express");
            }

            return frameworks.ToList();
        }

        private string DetectLanguage(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".cs" => "C#",
                ".py" => "Python",
                ".js" => "JavaScript",
                ".ts" => "TypeScript",
                ".java" => "Java",
                ".cpp" or ".cc" => "C++",
                ".c" => "C",
                ".h" or ".hpp" => "C/C++ Header",
                ".go" => "Go",
                ".rs" => "Rust",
                _ => "Unknown"
            };
        }

        private bool IsCommentLine(string line, string language)
        {
            return language switch
            {
                "C#" or "JavaScript" or "TypeScript" or "Java" or "C++" or "C" => line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*"),
                "Python" => line.StartsWith("#"),
                _ => false
            };
        }

        #endregion

        #region Symbol Extraction

        private List<CodeSymbol> ExtractSymbols(string content, string language)
        {
            var symbols = new List<CodeSymbol>();

            switch (language)
            {
                case "C#":
                    symbols.AddRange(ExtractCSharpSymbols(content));
                    break;
                case "Python":
                    symbols.AddRange(ExtractPythonSymbols(content));
                    break;
                case "JavaScript":
                case "TypeScript":
                    symbols.AddRange(ExtractJavaScriptSymbols(content));
                    break;
            }

            return symbols;
        }

        private List<CodeSymbol> ExtractCSharpSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Class detection
                var classMatch = Regex.Match(line, @"class\s+(\w+)");
                if (classMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = classMatch.Groups[1].Value, Type = SymbolType.Class, LineNumber = i + 1 });
                }

                // Method detection
                var methodMatch = Regex.Match(line, @"(public|private|protected|internal).*?\s+(\w+)\s*\(");
                if (methodMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = methodMatch.Groups[2].Value, Type = SymbolType.Method, LineNumber = i + 1 });
                }
            }

            return symbols;
        }

        private List<CodeSymbol> ExtractPythonSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Class detection
                var classMatch = Regex.Match(line, @"class\s+(\w+)");
                if (classMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = classMatch.Groups[1].Value, Type = SymbolType.Class, LineNumber = i + 1 });
                }

                // Function detection
                var funcMatch = Regex.Match(line, @"def\s+(\w+)\s*\(");
                if (funcMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = funcMatch.Groups[1].Value, Type = SymbolType.Function, LineNumber = i + 1 });
                }
            }

            return symbols;
        }

        private List<CodeSymbol> ExtractJavaScriptSymbols(string content)
        {
            var symbols = new List<CodeSymbol>();
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Class detection
                var classMatch = Regex.Match(line, @"class\s+(\w+)");
                if (classMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = classMatch.Groups[1].Value, Type = SymbolType.Class, LineNumber = i + 1 });
                }

                // Function detection
                var funcMatch = Regex.Match(line, @"function\s+(\w+)\s*\(");
                if (funcMatch.Success)
                {
                    symbols.Add(new CodeSymbol { Name = funcMatch.Groups[1].Value, Type = SymbolType.Function, LineNumber = i + 1 });
                }
            }

            return symbols;
        }

        #endregion

        #region Dependency Analysis

        private DependencyGraph BuildDependencyGraph(List<CodeFile> files)
        {
            var graph = new DependencyGraph();

            // Create nodes for each file
            foreach (var file in files)
            {
                var node = new DependencyNode
                {
                    Id = file.Path,
                    Name = Path.GetFileName(file.Path),
                    Type = NodeType.File,
                    FilePath = file.Path
                };
                graph.Nodes.Add(node);
            }

            // Detect circular dependencies (simplified)
            graph.CircularDependencies = DetectCircularDependencies(files);
            graph.MaxDepth = CalculateMaxDepth(graph.Nodes);

            return graph;
        }

        private List<string> DetectCircularDependencies(List<CodeFile> files)
        {
            var circular = new List<string>();
            // Simplified detection - in production, would use proper graph traversal
            return circular;
        }

        private int CalculateMaxDepth(List<DependencyNode> nodes)
        {
            // Simplified - in production, would traverse actual dependency tree
            return Math.Min(nodes.Count, 10);
        }

        #endregion

        #region Insights & Suggestions

        private ArchitecturalInsights GenerateInsights(Domain.ProjectAnalysis.ProjectAnalysis analysis)
        {
            var insights = new ArchitecturalInsights();

            // Detect architecture pattern
            var directories = analysis.Structure.Directories.Select(d => d.Name.ToLower()).ToList();
            if (directories.Contains("controllers") && directories.Contains("models") && directories.Contains("views"))
            {
                insights.ArchitecturePattern = "MVC";
            }
            else if (directories.Contains("domain") && directories.Contains("application") && directories.Contains("infrastructure"))
            {
                insights.ArchitecturePattern = "Clean Architecture";
            }

            // Calculate scores
            insights.MaintainabilityScore = CalculateMaintainabilityScore(analysis);
            insights.TestabilityScore = CalculateTestabilityScore(analysis);
            insights.ScalabilityScore = CalculateScalabilityScore(analysis);

            // Add strengths
            if (analysis.Files.Any(f => f.CommentLines > f.CodeLines * 0.2))
            {
                insights.Strengths.Add("Well-documented code");
            }
            if (analysis.Structure.Directories.Any(d => d.Type == DirectoryType.Test))
            {
                insights.Strengths.Add("Test coverage present");
            }

            return insights;
        }

        private List<RefactoringSuggestion> GenerateSuggestions(Domain.ProjectAnalysis.ProjectAnalysis analysis)
        {
            var suggestions = new List<RefactoringSuggestion>();

            // Check for complex files
            foreach (var file in analysis.Files.Where(f => f.ComplexityScore > 50))
            {
                suggestions.Add(new RefactoringSuggestion
                {
                    Title = "Reduce File Complexity",
                    Description = $"File has high complexity score ({file.ComplexityScore:F1})",
                    Type = RefactoringType.ReduceComplexity,
                    FilePath = file.Path,
                    Rationale = "High complexity makes code harder to maintain and test",
                    ImpactScore = 0.8,
                    EffortEstimate = 120
                });
            }

            // Check for documentation
            foreach (var file in analysis.Files.Where(f => f.CommentLines < 5 && f.CodeLines > 100))
            {
                suggestions.Add(new RefactoringSuggestion
                {
                    Title = "Add Documentation",
                    Description = "File lacks sufficient documentation",
                    Type = RefactoringType.AddDocumentation,
                    FilePath = file.Path,
                    Rationale = "Documentation improves code understanding",
                    ImpactScore = 0.5,
                    EffortEstimate = 30
                });
            }

            return suggestions;
        }

        #endregion

        #region Metrics

        private CodeMetrics CalculateMetrics(List<CodeFile> files)
        {
            var metrics = new CodeMetrics
            {
                TotalFiles = files.Count,
                TotalLines = files.Sum(f => f.LineCount),
                AverageComplexity = files.Any() ? files.Average(f => f.ComplexityScore) : 0,
                MaintainabilityIndex = CalculateMaintainabilityIndex(files)
            };

            // Language breakdown
            metrics.LanguageBreakdown = files
                .GroupBy(f => f.Language)
                .ToDictionary(g => g.Key, g => g.Sum(f => f.CodeLines));

            return metrics;
        }

        private double CalculateComplexity(string content)
        {
            // Simplified cyclomatic complexity
            var complexity = 1.0;
            complexity += Regex.Matches(content, @"\bif\b").Count;
            complexity += Regex.Matches(content, @"\bfor\b").Count;
            complexity += Regex.Matches(content, @"\bwhile\b").Count;
            complexity += Regex.Matches(content, @"\bcase\b").Count;
            complexity += Regex.Matches(content, @"\bcatch\b").Count;
            return complexity;
        }

        private double CalculateMaintainabilityScore(Domain.ProjectAnalysis.ProjectAnalysis analysis)
        {
            var score = 100.0;
            score -= analysis.Files.Average(f => f.ComplexityScore) * 0.5;
            score += analysis.Files.Average(f => (double)f.CommentLines / Math.Max(f.CodeLines, 1)) * 20;
            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateTestabilityScore(Domain.ProjectAnalysis.ProjectAnalysis analysis)
        {
            var hasTestDir = analysis.Structure.Directories.Any(d => d.Type == DirectoryType.Test);
            var score = hasTestDir ? 70.0 : 30.0;
            return score;
        }

        private double CalculateScalabilityScore(Domain.ProjectAnalysis.ProjectAnalysis analysis)
        {
            var score = 70.0;
            if (analysis.Dependencies.CircularDependencies.Any())
            {
                score -= 20;
            }
            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateMaintainabilityIndex(List<CodeFile> files)
        {
            if (!files.Any()) return 0;

            var avgComplexity = files.Average(f => f.ComplexityScore);
            var avgLines = files.Average(f => f.CodeLines);

            return Math.Max(0, Math.Min(100, 171 - 5.2 * Math.Log(avgLines) - 0.23 * avgComplexity));
        }

        #endregion

        #region Utility

        private long CalculateDirectorySize(string directoryPath)
        {
            try
            {
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                return files.Sum(f => new FileInfo(f).Length);
            }
            catch
            {
                return 0;
            }
        }

        public Domain.ProjectAnalysis.ProjectAnalysis? GetAnalysis(string analysisId)
        {
            return _analyses.TryGetValue(analysisId, out var analysis) ? analysis : null;
        }

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalAnalyses"] = _analyses.Count,
                ["totalFilesAnalyzed"] = _analyses.Values.Sum(a => a.Files.Count),
                ["languagesDetected"] = _analyses.Values.SelectMany(a => a.Languages).Distinct().Count()
            };
        }

        #endregion
    }
}
