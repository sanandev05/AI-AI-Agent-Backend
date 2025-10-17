namespace AI_AI_Agent.Domain.Workspace
{
    /// <summary>
    /// Represents an isolated workspace for a thread or project
    /// </summary>
    public class Workspace
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ThreadId { get; set; } = string.Empty;
        public string BasePath { get; set; } = string.Empty;
        public WorkspaceType Type { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
        public List<string> AllowedPaths { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public long SizeLimitBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
        public long CurrentSizeBytes { get; set; }
        public bool IsShared { get; set; }
        public List<string> SharedWith { get; set; } = new();
    }

    public enum WorkspaceType
    {
        Temporary,
        Project,
        Shared,
        Template
    }

    /// <summary>
    /// File structure within a workspace
    /// </summary>
    public class WorkspaceFile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkspaceId { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        public string? Content { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Workspace template for quick setup
    /// </summary>
    public class WorkspaceTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<TemplateFile> Files { get; set; } = new();
        public Dictionary<string, string> Variables { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    public class TemplateFile
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
    }

    /// <summary>
    /// Workspace sharing information
    /// </summary>
    public class WorkspaceShare
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string WorkspaceId { get; set; } = string.Empty;
        public string SharedBy { get; set; } = string.Empty;
        public string SharedWith { get; set; } = string.Empty;
        public WorkspacePermission Permission { get; set; }
        public DateTime SharedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }

    public enum WorkspacePermission
    {
        ReadOnly,
        ReadWrite,
        Admin
    }
}
