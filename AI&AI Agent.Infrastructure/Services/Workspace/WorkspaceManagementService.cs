using AI_AI_Agent.Domain.Workspace;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace AI_AI_Agent.Infrastructure.Services.Workspace
{
    /// <summary>
    /// Workspace management service for isolated per-thread workspaces
    /// </summary>
    public class WorkspaceManagementService
    {
        private readonly ILogger<WorkspaceManagementService> _logger;
        private readonly ConcurrentDictionary<string, Domain.Workspace.Workspace> _workspaces = new();
        private readonly ConcurrentDictionary<string, List<WorkspaceFile>> _workspaceFiles = new();
        private readonly ConcurrentDictionary<string, WorkspaceTemplate> _templates = new();
        private readonly ConcurrentDictionary<string, WorkspaceShare> _shares = new();
        private readonly string _baseWorkspacePath;

        public WorkspaceManagementService(ILogger<WorkspaceManagementService> logger)
        {
            _logger = logger;
            _baseWorkspacePath = Path.Combine(Directory.GetCurrentDirectory(), "workspaces");
            Directory.CreateDirectory(_baseWorkspacePath);
            InitializeDefaultTemplates();
        }

        #region Workspace Management

        public Domain.Workspace.Workspace CreateWorkspace(string name, string threadId, WorkspaceType type = WorkspaceType.Temporary)
        {
            var workspace = new Domain.Workspace.Workspace
            {
                Name = name,
                ThreadId = threadId,
                Type = type,
                BasePath = Path.Combine(_baseWorkspacePath, Guid.NewGuid().ToString())
            };

            Directory.CreateDirectory(workspace.BasePath);
            workspace.AllowedPaths.Add(workspace.BasePath);

            _workspaces[workspace.Id] = workspace;
            _workspaceFiles[workspace.Id] = new List<WorkspaceFile>();

            _logger.LogInformation("Created workspace {WorkspaceId} for thread {ThreadId}", workspace.Id, threadId);
            return workspace;
        }

        public Domain.Workspace.Workspace? GetWorkspace(string workspaceId)
        {
            if (_workspaces.TryGetValue(workspaceId, out var workspace))
            {
                workspace.LastAccessedAt = DateTime.UtcNow;
                return workspace;
            }
            return null;
        }

        public Domain.Workspace.Workspace? GetWorkspaceByThread(string threadId)
        {
            return _workspaces.Values.FirstOrDefault(w => w.ThreadId == threadId);
        }

        public List<Domain.Workspace.Workspace> GetAllWorkspaces()
        {
            return _workspaces.Values.ToList();
        }

        public bool DeleteWorkspace(string workspaceId)
        {
            if (_workspaces.TryRemove(workspaceId, out var workspace))
            {
                try
                {
                    if (Directory.Exists(workspace.BasePath))
                    {
                        Directory.Delete(workspace.BasePath, true);
                    }
                    _workspaceFiles.TryRemove(workspaceId, out _);
                    _logger.LogInformation("Deleted workspace {WorkspaceId}", workspaceId);
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete workspace {WorkspaceId}", workspaceId);
                    return false;
                }
            }
            return false;
        }

        #endregion

        #region File Operations

        public async Task<WorkspaceFile> AddFileAsync(string workspaceId, string fileName, string content, string? relativePath = null)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} not found");
            }

            var filePath = string.IsNullOrEmpty(relativePath)
                ? Path.Combine(workspace.BasePath, fileName)
                : Path.Combine(workspace.BasePath, relativePath, fileName);

            var directory = Path.GetDirectoryName(filePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content);

            var file = new WorkspaceFile
            {
                WorkspaceId = workspaceId,
                Path = filePath,
                FileName = fileName,
                ContentType = GetContentType(fileName),
                SizeBytes = content.Length,
                Content = content
            };

            if (_workspaceFiles.TryGetValue(workspaceId, out var files))
            {
                files.Add(file);
            }

            workspace.CurrentSizeBytes += file.SizeBytes;

            _logger.LogInformation("Added file {FileName} to workspace {WorkspaceId}", fileName, workspaceId);
            return file;
        }

        public async Task<string?> ReadFileAsync(string workspaceId, string fileName)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null) return null;

            var filePath = Path.Combine(workspace.BasePath, fileName);
            if (!File.Exists(filePath)) return null;

            return await File.ReadAllTextAsync(filePath);
        }

        public List<WorkspaceFile> GetFiles(string workspaceId)
        {
            return _workspaceFiles.TryGetValue(workspaceId, out var files) ? files : new List<WorkspaceFile>();
        }

        public bool DeleteFile(string workspaceId, string fileName)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null) return false;

            var filePath = Path.Combine(workspace.BasePath, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);

                if (_workspaceFiles.TryGetValue(workspaceId, out var files))
                {
                    var file = files.FirstOrDefault(f => f.FileName == fileName);
                    if (file != null)
                    {
                        files.Remove(file);
                        workspace.CurrentSizeBytes -= file.SizeBytes;
                    }
                }

                _logger.LogInformation("Deleted file {FileName} from workspace {WorkspaceId}", fileName, workspaceId);
                return true;
            }
            return false;
        }

        #endregion

        #region Templates

        private void InitializeDefaultTemplates()
        {
            // Python Data Science Template
            var pythonTemplate = new WorkspaceTemplate
            {
                Name = "Python Data Science",
                Description = "Python workspace with data science libraries",
                Files = new List<TemplateFile>
                {
                    new TemplateFile { Path = "main.py", Content = "# Python Data Science Project\nimport pandas as pd\nimport numpy as np\nimport matplotlib.pyplot as plt\n\n# Your code here\n" },
                    new TemplateFile { Path = "requirements.txt", Content = "pandas\nnumpy\nmatplotlib\nscikit-learn\n" },
                    new TemplateFile { Path = "data", Content = "", IsDirectory = true },
                    new TemplateFile { Path = "notebooks", Content = "", IsDirectory = true }
                },
                Tags = new List<string> { "python", "data-science", "ml" }
            };
            _templates[pythonTemplate.Id] = pythonTemplate;

            // Web Project Template
            var webTemplate = new WorkspaceTemplate
            {
                Name = "Web Project",
                Description = "Basic web project structure",
                Files = new List<TemplateFile>
                {
                    new TemplateFile { Path = "index.html", Content = "<!DOCTYPE html>\n<html>\n<head>\n    <title>{{projectName}}</title>\n</head>\n<body>\n    <h1>Welcome</h1>\n</body>\n</html>" },
                    new TemplateFile { Path = "css", Content = "", IsDirectory = true },
                    new TemplateFile { Path = "js", Content = "", IsDirectory = true }
                },
                Tags = new List<string> { "web", "html", "javascript" }
            };
            _templates[webTemplate.Id] = webTemplate;

            _logger.LogInformation("Initialized {Count} default workspace templates", _templates.Count);
        }

        public Domain.Workspace.Workspace CreateFromTemplate(string templateId, string name, string threadId, Dictionary<string, string>? variables = null)
        {
            if (!_templates.TryGetValue(templateId, out var template))
            {
                throw new InvalidOperationException($"Template {templateId} not found");
            }

            var workspace = CreateWorkspace(name, threadId, WorkspaceType.Project);

            foreach (var templateFile in template.Files)
            {
                if (templateFile.IsDirectory)
                {
                    var dirPath = Path.Combine(workspace.BasePath, templateFile.Path);
                    Directory.CreateDirectory(dirPath);
                }
                else
                {
                    var content = templateFile.Content;
                    if (variables != null)
                    {
                        foreach (var (key, value) in variables)
                        {
                            content = content.Replace($"{{{{{key}}}}}", value);
                        }
                    }

                    var filePath = Path.Combine(workspace.BasePath, templateFile.Path);
                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != null)
                    {
                        Directory.CreateDirectory(directory);
                    }
                    File.WriteAllText(filePath, content);
                }
            }

            _logger.LogInformation("Created workspace {WorkspaceId} from template {TemplateName}", workspace.Id, template.Name);
            return workspace;
        }

        public List<WorkspaceTemplate> GetTemplates()
        {
            return _templates.Values.ToList();
        }

        public WorkspaceTemplate? GetTemplate(string templateId)
        {
            return _templates.TryGetValue(templateId, out var template) ? template : null;
        }

        #endregion

        #region Sharing

        public WorkspaceShare ShareWorkspace(string workspaceId, string sharedBy, string sharedWith, WorkspacePermission permission, DateTime? expiresAt = null)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null)
            {
                throw new InvalidOperationException($"Workspace {workspaceId} not found");
            }

            var share = new WorkspaceShare
            {
                WorkspaceId = workspaceId,
                SharedBy = sharedBy,
                SharedWith = sharedWith,
                Permission = permission,
                ExpiresAt = expiresAt
            };

            _shares[share.Id] = share;
            workspace.IsShared = true;
            workspace.SharedWith.Add(sharedWith);

            _logger.LogInformation("Shared workspace {WorkspaceId} with {User}", workspaceId, sharedWith);
            return share;
        }

        public List<WorkspaceShare> GetWorkspaceShares(string workspaceId)
        {
            return _shares.Values.Where(s => s.WorkspaceId == workspaceId).ToList();
        }

        public bool RevokeShare(string shareId)
        {
            if (_shares.TryRemove(shareId, out var share))
            {
                _logger.LogInformation("Revoked share {ShareId} for workspace {WorkspaceId}", shareId, share.WorkspaceId);
                return true;
            }
            return false;
        }

        public bool HasPermission(string workspaceId, string userId, WorkspacePermission requiredPermission)
        {
            var shares = GetWorkspaceShares(workspaceId);
            var userShare = shares.FirstOrDefault(s => s.SharedWith == userId);

            if (userShare == null) return false;
            if (userShare.ExpiresAt.HasValue && userShare.ExpiresAt.Value < DateTime.UtcNow) return false;

            return userShare.Permission >= requiredPermission;
        }

        #endregion

        #region Organization

        public async Task OrganizeFilesAsync(string workspaceId)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null) return;

            var files = GetFiles(workspaceId);

            // Organize by file type
            var groupedFiles = files.GroupBy(f => GetFileCategory(f.FileName));

            foreach (var group in groupedFiles)
            {
                var categoryDir = Path.Combine(workspace.BasePath, group.Key);
                Directory.CreateDirectory(categoryDir);

                foreach (var file in group)
                {
                    var oldPath = file.Path;
                    var newPath = Path.Combine(categoryDir, file.FileName);

                    if (File.Exists(oldPath) && oldPath != newPath)
                    {
                        File.Move(oldPath, newPath);
                        file.Path = newPath;
                    }
                }
            }

            _logger.LogInformation("Organized files in workspace {WorkspaceId}", workspaceId);
        }

        private string GetFileCategory(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".py" or ".js" or ".ts" or ".cs" or ".java" => "code",
                ".txt" or ".md" or ".doc" or ".docx" => "documents",
                ".jpg" or ".png" or ".gif" or ".bmp" => "images",
                ".csv" or ".xlsx" or ".json" => "data",
                _ => "other"
            };
        }

        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLower();
            return extension switch
            {
                ".txt" => "text/plain",
                ".md" => "text/markdown",
                ".py" => "text/x-python",
                ".js" => "text/javascript",
                ".json" => "application/json",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }

        #endregion

        #region Statistics

        public Dictionary<string, object> GetStatistics()
        {
            return new Dictionary<string, object>
            {
                ["totalWorkspaces"] = _workspaces.Count,
                ["temporaryWorkspaces"] = _workspaces.Values.Count(w => w.Type == WorkspaceType.Temporary),
                ["projectWorkspaces"] = _workspaces.Values.Count(w => w.Type == WorkspaceType.Project),
                ["sharedWorkspaces"] = _workspaces.Values.Count(w => w.IsShared),
                ["totalFiles"] = _workspaceFiles.Values.Sum(files => files.Count),
                ["totalSizeMB"] = _workspaces.Values.Sum(w => w.CurrentSizeBytes) / (1024.0 * 1024.0),
                ["templates"] = _templates.Count,
                ["activeShares"] = _shares.Count
            };
        }

        public Dictionary<string, object> GetWorkspaceStatistics(string workspaceId)
        {
            var workspace = GetWorkspace(workspaceId);
            if (workspace == null)
            {
                return new Dictionary<string, object> { ["error"] = "Workspace not found" };
            }

            var files = GetFiles(workspaceId);

            return new Dictionary<string, object>
            {
                ["workspaceId"] = workspaceId,
                ["name"] = workspace.Name,
                ["type"] = workspace.Type.ToString(),
                ["fileCount"] = files.Count,
                ["sizeMB"] = workspace.CurrentSizeBytes / (1024.0 * 1024.0),
                ["sizeLimit MB"] = workspace.SizeLimitBytes / (1024.0 * 1024.0),
                ["isShared"] = workspace.IsShared,
                ["sharedWithCount"] = workspace.SharedWith.Count,
                ["createdAt"] = workspace.CreatedAt,
                ["lastAccessedAt"] = workspace.LastAccessedAt
            };
        }

        #endregion
    }
}
