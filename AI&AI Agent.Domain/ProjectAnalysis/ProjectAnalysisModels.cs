namespace AI_AI_Agent.Domain.ProjectAnalysis
{
    /// <summary>
    /// Represents analysis of a codebase or project
    /// </summary>
    public class ProjectAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectPath { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
        public List<string> Languages { get; set; } = new();
        public List<string> Frameworks { get; set; } = new();
        public ProjectStructure Structure { get; set; } = new();
        public List<CodeFile> Files { get; set; } = new();
        public DependencyGraph Dependencies { get; set; } = new();
        public ArchitecturalInsights Insights { get; set; } = new();
        public List<RefactoringSuggestion> Suggestions { get; set; } = new();
        public CodeMetrics Metrics { get; set; } = new();
    }

    /// <summary>
    /// Project directory structure
    /// </summary>
    public class ProjectStructure
    {
        public string RootPath { get; set; } = string.Empty;
        public List<ProjectDirectory> Directories { get; set; } = new();
        public int TotalFiles { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class ProjectDirectory
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public DirectoryType Type { get; set; }
    }

    public enum DirectoryType
    {
        Source,
        Test,
        Configuration,
        Documentation,
        Build,
        Resources,
        Other
    }

    /// <summary>
    /// Individual code file analysis
    /// </summary>
    public class CodeFile
    {
        public string Path { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public int LineCount { get; set; }
        public int CodeLines { get; set; }
        public int CommentLines { get; set; }
        public int BlankLines { get; set; }
        public List<CodeSymbol> Symbols { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public double ComplexityScore { get; set; }
    }

    public class CodeSymbol
    {
        public string Name { get; set; } = string.Empty;
        public SymbolType Type { get; set; }
        public int LineNumber { get; set; }
        public List<string> References { get; set; } = new();
    }

    public enum SymbolType
    {
        Class,
        Interface,
        Method,
        Function,
        Property,
        Variable,
        Constant,
        Enum
    }

    /// <summary>
    /// Dependency graph for the project
    /// </summary>
    public class DependencyGraph
    {
        public List<DependencyNode> Nodes { get; set; } = new();
        public List<DependencyEdge> Edges { get; set; } = new();
        public List<string> CircularDependencies { get; set; } = new();
        public int MaxDepth { get; set; }
    }

    public class DependencyNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }

    public enum NodeType
    {
        Package,
        Module,
        Class,
        File
    }

    public class DependencyEdge
    {
        public string FromNodeId { get; set; } = string.Empty;
        public string ToNodeId { get; set; } = string.Empty;
        public DependencyType Type { get; set; }
    }

    public enum DependencyType
    {
        Import,
        Inheritance,
        Composition,
        Usage
    }

    /// <summary>
    /// Architectural insights about the project
    /// </summary>
    public class ArchitecturalInsights
    {
        public string ArchitecturePattern { get; set; } = string.Empty;
        public List<string> DesignPatterns { get; set; } = new();
        public List<ArchitectureIssue> Issues { get; set; } = new();
        public List<string> Strengths { get; set; } = new();
        public double MaintainabilityScore { get; set; }
        public double TestabilityScore { get; set; }
        public double ScalabilityScore { get; set; }
    }

    public class ArchitectureIssue
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IssueSeverity Severity { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public List<string> AffectedComponents { get; set; } = new();
    }

    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Refactoring suggestions
    /// </summary>
    public class RefactoringSuggestion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RefactoringType Type { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }
        public string Rationale { get; set; } = string.Empty;
        public double ImpactScore { get; set; }
        public int EffortEstimate { get; set; } // minutes
    }

    public enum RefactoringType
    {
        ExtractMethod,
        RenameVariable,
        SimplifyExpression,
        RemoveDuplication,
        ImproveNaming,
        AddDocumentation,
        ReduceComplexity,
        ImproveSecurity,
        OptimizePerformance
    }

    /// <summary>
    /// Code quality metrics
    /// </summary>
    public class CodeMetrics
    {
        public int TotalLines { get; set; }
        public int TotalFiles { get; set; }
        public double AverageComplexity { get; set; }
        public double CodeCoverage { get; set; }
        public int TechnicalDebtMinutes { get; set; }
        public double MaintainabilityIndex { get; set; }
        public Dictionary<string, int> LanguageBreakdown { get; set; } = new();
    }
}
