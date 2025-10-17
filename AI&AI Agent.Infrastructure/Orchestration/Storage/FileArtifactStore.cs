using AI_AI_Agent.Application;
using AI_AI_Agent.Domain.Events;

namespace AI_AI_Agent.Infrastructure.Orchestration.Storage;

public sealed class FileArtifactStore : IArtifactStore
{
    private readonly string _root;
    public FileArtifactStore()
    {
        _root = Path.Combine(AppContext.BaseDirectory, "storage");
        Directory.CreateDirectory(_root);
    }
    public Task<Artifact> SaveAsync(Guid runId, string stepId, string localPath, string? fileName=null, string? mime=null)
    {
        var dir = Path.Combine(_root, runId.ToString()); Directory.CreateDirectory(dir);
        var destName = fileName ?? Path.GetFileName(localPath);
        var dest = Path.Combine(dir, destName); File.Copy(localPath, dest, true);
        var fi = new FileInfo(dest);
        return Task.FromResult(new Artifact(fi.Name, fi.FullName, mime ?? MimeFromExt(fi.Extension), fi.Length));
    }
    private static string MimeFromExt(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        _ => "application/octet-stream"
    };
}
